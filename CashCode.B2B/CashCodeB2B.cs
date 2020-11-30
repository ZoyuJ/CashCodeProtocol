namespace CashCodeProtocol {
  using System;
  using System.Collections.Generic;
  using System.IO.Ports;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Logging;

  public class CashCodeB2B : IDisposable {

    public CashCodeB2B(CashCounterCfg Cfg, ILogger<CashCodeB2B> Logger) {
      _Cfg = Cfg;
      _CTkS = new CancellationTokenSource();
      _SerialPort = new SerialPort(_Cfg.DecicePort);
    }
    /*
     ◆NO RESPONSE
        Controller   CashCode
            ┃   Command  ┃
            ┣━━━━━━━━━━━▶┃
            ┃            ┃
            ┃   ACK      ┃
            ┃◀━━━━━━━━━━━┫
            ┃            ┃
     ◆Has RESPONSE
        Controller   CashCode
          ━━┫   Command  ┃
         ▲  ┣━━━━━━━━━━━▶┃
            ┃   Data     ┃
  100-200ms ┃◀━━━━━━━━━━━┫
            ┃   ACK      ┃
         ▼  ┣━━━━━━━━━━━▶┃
          ━━┫Next Command┃
            ┣━━━━━━━━━━━▶┃
            ┃            ┃
      ◆Controller Found CRC Failed
        Controller   CashCode
          ━━┫    Command      ┃
         ▲  ┣━━━━━━━━━━━━━━━━▶┃
            ┃ Data,CRC Failed ┃
  100-200ms ┃◀━━━━━━━━━━━━━━━━┫
            ┃    NAK          ┃
         ▼  ┣━━━━━━━━━━━━━━━━▶┣
            ┃    Command      ┃ <=10ms
          ━━╋━━━━━━━━━━━━━━━━▶┣
            ┃    Data         ┃
            ┃◀━━━━━━━━━━━━━━━━┫
            ┃    ACK          ┃
            ┣━━━━━━━━━━━━━━━━▶┃


     */
    protected readonly CancellationTokenSource _CTkS;
    protected readonly SerialPort _SerialPort;
    protected readonly CashCounterCfg _Cfg;

    protected readonly static byte DEVICE_TYPE = 0x03;

    public virtual void Connect() {
      _SerialPort.BaudRate = 9600;
      _SerialPort.DataBits = 8;
      _SerialPort.StopBits = StopBits.One;
      _SerialPort.Parity = Parity.None;
      _SerialPort.Open();
    }

    protected const int POLLDELAY = 200;
    protected const int NAKDELAY = 5;
    protected const int ACKDELAY = 200;
    protected const int TIMEDOUT = 100000;
    public virtual bool EnablePolling { get; protected set; } = true;
    public virtual async Task StartPollingLoop() {
      SendReset();
      SendEnableBillTypes(_Cfg.EnableCashType);
      SendPoll();
      while (!_CTkS.IsCancellationRequested) {
        if (_PackageQueue.Count > 0) {
          var Package = _PackageQueue.Dequeue();
          _SerialPort.Write(Package, 0, Package.Length);
          var State = DataReceived();
          byte[] Pack = null;
          switch (State) {
            case RecivedState.ACK:
              Pack = GeneratPackage(0x00, new byte[] { });
              _SerialPort.Write(Pack, 0, Pack.Length);
              _ReadOffset = 0;
              break;
            case RecivedState.Data:
              Pack = GeneratPackage(0x00, new byte[] { });
              _SerialPort.Write(Pack, 0, Pack.Length);
              OnPackageRecived();
              _ReadOffset = 0;
              break;
            default:
            case RecivedState.NAK:
              _SerialPort.Write(Package, 0, Package.Length);
              break;
            case RecivedState.CRCFail:
              Pack = GeneratPackage(0xFF, new byte[] { });
              _SerialPort.Write(Pack, 0, Pack.Length);
              await Task.Delay(NAKDELAY);
              _SerialPort.Write(Package, 0, Package.Length);
              break;
            case RecivedState.TimedOut:
            case RecivedState.Recving:
              break;
          }

        }
        await Task.Delay(POLLDELAY);
      }
      this.Dispose();
    }

    public void StopPolling() => EnablePolling = false;
    public void StartPolling() => EnablePolling = true;

    public void SendNAK() => EnqueuePacket(GeneratPackage(0xFF, new byte[] { }));
    public void SendACK() => EnqueuePacket(GeneratPackage(0x00, new byte[] { }));
    public void SendStack() => EnqueuePacket(GeneratPackage(0x35, new byte[] { }));
    public void SendReset() => EnqueuePacket(GeneratPackage(0x30, new byte[] { }));
    public void SendEnableBillTypes(byte value) => EnqueuePacket(GeneratPackage(0x34, new byte[] { 0, 0, value, 0, 0, 0 }));

    public void SendReturn() => EnqueuePacket(GeneratPackage(0x36, new byte[] { }));
    public void SendPoll() => EnqueuePacket(GeneratPackage(0x33, new byte[] { }));
    public void SendIdentification() => EnqueuePacket(GeneratPackage(0x37, new byte[] { }));
    public void SendStatus() => EnqueuePacket(GeneratPackage(0x31, new byte[] { }));
    public void SendSecurity(byte value) => EnqueuePacket(GeneratPackage(0x32, new byte[] { 0, 0, value }));

    protected virtual byte[] GeneratPackage(byte command, byte[] data) {
      int Len = data.Length + 6;
      byte[] CommandArr = new byte[data.Length + 4 + 2];
      CommandArr[0] = 0x02;   //sync
      CommandArr[1] = 0x03;   //valid address
      CommandArr[2] = (byte)Len; //length
      CommandArr[3] = command; //command
      Array.Copy(data, 0, CommandArr, 4, data.Length);
      int crcValue = Crc16(CommandArr, 0, CommandArr.Length - 2);
      CommandArr[Len - 1] = (byte)((crcValue >> 8) & 0xFF);
      CommandArr[Len - 2] = (byte)(crcValue & 0xFF);
      return CommandArr;
    }
    protected virtual void EnqueuePacket(in byte[] Packet) {
      _PackageQueue.Enqueue(Packet);
      //Console.WriteLine($"Q ue ue : {Common.Kits.ByteArrayToHexString(Packet.ToArray())}");
    }
    protected readonly Queue<byte[]> _PackageQueue = new Queue<byte[]>();
    /// <summary>
    /// |0          |1                  |2                   |3    n|n+1 n+2|
    /// |SYNC =0x02 |Address =DeviceType|Len =FullLen-CRCLen |Data  |CRC16  |
    /// </summary>
    protected readonly byte[] _RecvBuffer = new byte[1024];
    protected int _ReadOffset = 0;
    protected virtual RecivedState DataReceived() {
      long StartTick = DateTime.Now.Ticks;
      while (!_CTkS.IsCancellationRequested) {
        if (DateTime.Now.Ticks - StartTick > TIMEDOUT) return RecivedState.TimedOut;
        var Rd = _SerialPort.ReadByte();
        if (Rd == -1) continue;
        if (Rd == 0x02) {
          if (_ReadOffset != 0) {
            if (_RecvBuffer[2] == _ReadOffset) {
              var CRC = Crc16(_RecvBuffer, 0, _ReadOffset - 2);
              if (_RecvBuffer[_ReadOffset - 1] == (byte)(CRC >> 8 & 0xFF)
                && _RecvBuffer[_ReadOffset - 2] == (byte)(CRC & 0xFF)) {
                //Console.WriteLine($"Recived : { Common.Kits.ByteArrayToHexString(_RecvBuffer.Take(_ReadOffset).ToArray())}");
                if (_RecvBuffer[3] == 0xFF) {
                  //NAK Package
                  _ReadOffset = 0;
                  return RecivedState.NAK;
                }
                else if (_RecvBuffer[3] == 0x00) {
                  //ACK Package
                  return RecivedState.ACK;
                }
                else {
                  //Responsed Package
                  return RecivedState.Data;
                }
              }
              else {
                //Console.WriteLine($"\tExp: CRC ({_RecvBuffer[_ReadOffset - 1]} ? {(byte)(CRC >> 8 & 0xFF) },{_RecvBuffer[_ReadOffset - 2]} ? {(byte)(CRC & 0xFF)}) Not Match");
                return RecivedState.CRCFail;
              }
            }
            //else {
            //  Console.WriteLine($"\tExp: Len{_RecvBuffer[2]} Not Match {_ReadOffset}");
            //  return false;
            //}
            _ReadOffset = 0;
          }
        }
        _RecvBuffer[_ReadOffset] = (byte)Rd;
        _ReadOffset++;
      }
      return RecivedState.TimedOut;
    }

    protected static int Crc16(byte[] arr, in int Offset, in int Length) {
      int i = Offset, tmpCrc = 0;
      byte j;
      for (; i < Length; i++) {
        tmpCrc ^= arr[i];
        for (j = 0; j <= 7; j++) {
          if ((tmpCrc & 0x0001) != 0) {
            tmpCrc >>= 1;
            tmpCrc ^= 0x08408;
          }
          else {
            tmpCrc >>= 1;
          }
        }
      }
      return tmpCrc;
    }

    protected virtual void OnPackageRecived() {
      OnRecivedHandler?.Invoke(
        (PollRecivedPackageType)((_RecvBuffer[3] << 8 & 0xFF) & (_RecvBuffer[4] & 0xFF)),
        _RecvBuffer[4],
        _RecvBuffer.Skip(3).Take(_ReadOffset - 2 - 3).ToArray());
    }

    /// <summary>
    /// T1:MainType T2:SubType(if has else 0x00) T3:FullData[0:MainType 1:SubType 2-n:Data],no crc
    /// </summary>
    public event Action<PollRecivedPackageType, byte, byte[]> OnRecivedHandler;

    protected enum RecivedState {
      Recving = 0,
      TimedOut = -3,
      CRCFail = -2,
      NAK = -1,
      ACK = 1,
      Data = 2,
    }
    /// <summary>
    /// Code|SubCode
    /// </summary>
    public enum PollRecivedPackageType {
      PowerUp = 0x1000 | 0x0000,
      PowerUpWithBillInValidator = 0x1100 | 0x0000,
      PowerUpWithBillInChassis = 0x1200 | 0x0000,
      Initialize = 0x1300 | 0x0000,
      Idling = 0x1300 | 0x0000,
      Accepting = 0x1400 | 0x0000,
      Stacking = 0x1700 | 0x0000,
      Returning = 0x1800 | 0x0000,
      Disabled = 0x1900 | 0x0000,
      Holding = 0x1A00 | 0x0000,
      Busy = 0x1B00 | 0x0000,

      Rejected_Insertion = 0x1C00 | 0x0060,
      Rejected_Magnetic = 0x1C00 | 0x0061,
      Rejected_Bill = 0x1C00 | 0x0062,
      Rejected_Multiply = 0x1C00 | 0x0063,
      Rejected_Conveying = 0x1C00 | 0x0064,
      Rejected_Identification = 0x1C00 | 0x0065,
      Rejected_Verification = 0x1C00 | 0x0066,
      Rejected_Optic = 0x1C00 | 0x0067,
      Rejected_Inhibit = 0x1C00 | 0x0068,
      Rejected_Capacity = 0x1C00 | 0x0069,
      Rejected_Operation = 0x1C00 | 0x006A,
      Rejected_Length = 0x1C00 | 0x006C,
      Rejected_UV = 0x1C00 | 0x006D,
      Rejected_Barcode_Unrecognized = 0x1C00 | 0x0092,
      Rejected_Barcode_IncorrectNum = 0x1C00 | 0x0093,
      Rejected_Barcode_UnknownStart = 0x1C00 | 0x0094,
      Rejected_Barcode_UnknownStop = 0x1C00 | 0x0095,

      Dispensing_Recycling2Dispenser = 0x1D00 | 0x0000,
      Dispensing_WaittingCustomeTake = 0x1D00 | 0x0001,

      Unloading_Recycling2Drop = 0x1E00 | 0x0000,
      Unloading_Recycling2Drop_TooMuchBills = 0x1E00 | 0x0001,

      SettingTypeCassette = 0x2100 | 0x0000,
      Dispensed = 0x2500 | 0x0000,

      Unloaded = 0x2600 | 0x0000,

      InvalidBillNumber = 0x2800 | 0x0000,
      SettedTypeCassette = 0x2900 | 0x0000,

      InvalidCommand = 0x3000 | 0x0000,

      DropCassetteFull = 0x4100 | 0x0000,
      DropCassetteRemoved = 0x4200 | 0x0000,

      JammedInAcceptor = 0x4300 | 0x0000,
      JammedInStacker = 0x4400 | 0x0000,

      Cheated = 0x4500 | 0x0000,

      GenericErrorCode = 0x4700 | 0x0000,
      /// <summary>
      /// SubCode:BillType
      /// </summary>
      ESCROW = 0x8000 | 0x0000,
      /// <summary>
      /// SubCode:BillType
      /// Data:L=1B
      /// </summary>
      PackedOrStacked = 0x8100 | 0x0000,

      Returned = 0x8200 | 0x0000,

      WaittingOfDecision = 0x8300 | 0x0100,
    }

    public void Dispose() {
      try { _SerialPort.Dispose(); } catch { }
    }
  }
  public class CashCounterCfg {
    public byte DeviceType { get; set; } = 0x03;
    /// <summary>
    /// default=0b11111111
    /// 低
    ///   Index Description(1=Enable,0=Disable)
    ///   0     CNY1
    ///   1     CNY2
    ///   2     CNY5
    ///   3     CNY10
    ///   4     CNY20
    ///   5     CNY50
    ///   6     CNY100
    ///   7     <Reserv,Set 1>
    ///   ...   <Reserv,Set 1>
    /// 高
    /// Example: disable CNY1:0XFE=0b11111110 Enable all :0xFF=0b11111111
    /// </summary>
    public byte EnableCashType { get; set; } = 0xFF;
    public string DecicePort { get; set; }
  }
}
