namespace CashCodeProtocol {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO.Ports;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Logging;

  public class CashCodeB2B : IDisposable {


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
        Controller        CashCode
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
      ◆Client Found CRC Failed
        Controller        CashCode
          ━━┫    Command      ┃
         ▲  ┣━━━━━━━━━━━━━━━━▶┃
100-200ms   ┃    NAK          ┃
            ┃◀━━━━━━━━━━━━━━━━┫
         ▼  ┃    Command      ┃ <=10ms
          ━━╋━━━━━━━━━━━━━━━━▶┣
            ┃    Data         ┃
            ┃◀━━━━━━━━━━━━━━━━┫
            ┃    ACK          ┃
            ┣━━━━━━━━━━━━━━━━▶┃

     */
    public CashCodeB2B(CashCodeB2BCfg Cfg, ILogger<CashCodeB2B> Logger) {
      _Logger = Logger;
      _Cfg = Cfg;
      _CTkS = new CancellationTokenSource();
      _SerialPort = new SerialPort(_Cfg.DecicePort);
    }
    protected readonly ILogger<CashCodeB2B> _Logger;
    protected readonly CancellationTokenSource _CTkS;
    protected readonly SerialPort _SerialPort;
    protected readonly CashCodeB2BCfg _Cfg;

    protected readonly static byte DEVICE_TYPE = 0x03;

    /// <summary>
    /// Connect to serial port
    /// </summary>
    public virtual void Connect() {
      _SerialPort.BaudRate = 9600;
      _SerialPort.DataBits = 8;
      _SerialPort.StopBits = StopBits.One;
      _SerialPort.Parity = Parity.None;
      _SerialPort.Open();
    }

    /// <summary>
    /// default delay timespan(ms) between every commands
    /// </summary>
    protected const int POLLDELAY = 200;
    /// <summary>
    /// ignore this,anytime u sent NAK package,must sent Command again in 10ms,in this impl just sent the command immediately
    /// </summary>
    protected const int NAKDELAY = 5;
    /// <summary>
    /// if got ack,then send next command,so this value is equal to 'POLLDELAY'
    /// </summary>
    protected const int ACKDELAY = 200;
    /// <summary>
    /// in document,device will response in 100000Ticks,but always Timedout, maybe 'Tick' in document is not equal to c#'s
    /// </summary>
    protected const int TIMEDOUT = 100000;
    public virtual bool EnablePolling { get; protected set; } = true;
    /// <summary>
    /// Enqueue Reset,then Enqueue EnableBillTypes,then Enqueue Poll,get in loop,
    /// if Queue has no items,enqueue a Poll request automatically
    /// </summary>
    /// <returns></returns>
    public virtual async Task StartPollingLoop() {
      SendReset();
      SendEnableBillTypes(_Cfg.EnableCashType);
      SendPoll();
      while (!_CTkS.IsCancellationRequested) {
        if (_PackageQueue.Count > 0) {
          var Package = _PackageQueue.Peek();
          _SerialPort.Write(Package.CommandData, 0, Package.CommandData.Length);
          var State = DataReceived();
          switch (State) {
            case RecivedState.ACK:
              _PackageQueue.Dequeue();
              _ReadOffset = 0;
              //Package.IsACK = true;
              await Task.Delay(POLLDELAY);
              break;
            case RecivedState.Data:
              SendPacket(0x00, new byte[] { });
              Package.ResponseData = _RecvBuffer;
              Package.ResponsDataLength = _ReadOffset;
              OnPackageRecived(Package);
              _PackageQueue.Dequeue();
              _ReadOffset = 0;
              await Task.Delay(POLLDELAY);
              break;

            case RecivedState.NAK:
              _SerialPort.Write(Package.CommandData, 0, Package.CommandData.Length);
              break;
            case RecivedState.CRCFail:
              SendPacket(0xFF, new byte[] { });
              //await Task.Delay(NAKDELAY);
              //_SerialPort.Write(Package, 0, Package.Length);
              break;
            case RecivedState.Cancelled:
            case RecivedState.TimedOut:
            case RecivedState.Recving:
              break;
          }

        }
        else {
          SendPoll();
        }
      }
      this.Dispose();
    }

    /// <summary>
    /// if command has no data
    /// </summary>
    protected static readonly byte[] EMPTYDATAARRAY = new byte[0];

    public void SendStack() => EnqueuePacket(GeneratPackage(CommandMark.Stack, EMPTYDATAARRAY));
    public void SendReset() => EnqueuePacket(GeneratPackage(CommandMark.Reset, EMPTYDATAARRAY));
    public void SendEnableBillTypes(byte value) => EnqueuePacket(GeneratPackage(CommandMark.EnableBillTypes, new byte[] { 0, 0, value, 0, 0, 0 }));

    public void SendReturn() => EnqueuePacket(GeneratPackage(CommandMark.Return, EMPTYDATAARRAY));
    public void SendPoll() => EnqueuePacket(GeneratPackage(CommandMark.Poll, EMPTYDATAARRAY));
    public void SendIdentification() => EnqueuePacket(GeneratPackage(CommandMark.Identification, EMPTYDATAARRAY));
    public void SendStatus() => EnqueuePacket(GeneratPackage(CommandMark.GetStatus, EMPTYDATAARRAY));
    public void SendSecurity(byte value) => EnqueuePacket(GeneratPackage(CommandMark.SetSecurity, EMPTYDATAARRAY));

    /// <summary>
    /// generate packe,fill LEN,CRC
    /// </summary>
    /// <param name="Command"></param>
    /// <param name="Data"></param>
    /// <returns></returns>
    protected virtual Command GeneratPackage(byte Command, byte[] Data) {
      int Len = Data.Length + 6;
      byte[] CommandArr = new byte[Data.Length + 4 + 2];
      CommandArr[0] = 0x02;   //sync
      CommandArr[1] = 0x03;   //valid address
      CommandArr[2] = (byte)Len; //length
      CommandArr[3] = (byte)Command; //command
      Array.Copy(Data, 0, CommandArr, 4, Data.Length);
      int crcValue = Crc16(CommandArr, 0, CommandArr.Length - 2);
      CommandArr[Len - 1] = (byte)((crcValue >> 8) & 0xFF);
      CommandArr[Len - 2] = (byte)(crcValue & 0xFF);
      return new Command() { CommandData = CommandArr, CommandMark = null };
    }
    /// <summary>
    /// generate packe,fill LEN,CRC
    /// </summary>
    /// <param name="Command"></param>
    /// <param name="Data"></param>
    /// <returns></returns>
    protected virtual Command GeneratPackage(CommandMark Command, byte[] Data) {
      var CMDPack = GeneratPackage((byte)Command, Data);
      CMDPack.CommandMark = Command;
      return CMDPack;
    }
    /// <summary>
    /// enqueue command
    /// </summary>
    /// <param name="Packet"></param>
    protected virtual void EnqueuePacket(in Command Packet) => _PackageQueue.Enqueue(Packet);
    /// <summary>
    /// send package driectly,use this method to send ACK,NAK
    /// </summary>
    /// <param name="Command"></param>
    /// <param name="data"></param>
    public virtual void SendPacket(byte Command, byte[] data) {
      var Cmd = GeneratPackage(Command, data);
      _SerialPort.Write(Cmd.CommandData, 0, Cmd.CommandData.Length);
    }

    /// <summary>
    /// Commands queue waitting for request to device
    /// </summary>
    protected readonly Queue<Command> _PackageQueue = new Queue<Command>();
    /// <summary>
    /// |0          |1                  |2                   |3    n|n+1 n+2|
    /// |SYNC =0x02 |Address =DeviceType|Len =FullLen-CRCLen |Data  |CRC16  |
    /// </summary>
    protected readonly byte[] _RecvBuffer = new byte[1024];
    protected int _ReadOffset = 0;
    /// <summary>
    /// read data from SerialPort received buffer
    /// </summary>
    /// <returns></returns>
    protected virtual RecivedState DataReceived() {
      long StartTick = DateTime.Now.Ticks;
      while (!_CTkS.IsCancellationRequested) {
        //if (DateTime.Now.Ticks - StartTick > TIMEDOUT) return RecivedState.TimedOut;
        var Rd = _SerialPort.ReadByte();
        if (Rd == -1) continue;
        _RecvBuffer[_ReadOffset] = (byte)Rd;
        _ReadOffset++;
        if (_ReadOffset >= 2) {
          if (_RecvBuffer[2] == _ReadOffset) {
            var CRC = Crc16(_RecvBuffer, 0, _ReadOffset - 2);
            if (_RecvBuffer[_ReadOffset - 1] == (byte)(CRC >> 8 & 0xFF)
              && _RecvBuffer[_ReadOffset - 2] == (byte)(CRC & 0xFF)) {
              //Console.WriteLine($"Recived : { Common.Kits.ByteArrayToHexString(_RecvBuffer.Take(_ReadOffset).ToArray())}");
              if (_RecvBuffer[3] == 0xFF) {
                //NAK Package
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

        }

      }
      return RecivedState.Cancelled;
    }

    protected virtual void OnPackageRecived(in Command Package) {
      OnRecivedHandler?.Invoke(Package);
    }
    /// <summary>
    /// T1:MainType T2:SubType(if has else 0x00) T3:FullData[0:MainType 1:SubType 2-n:Data],no crc
    /// </summary>
    public event Action<Command> OnRecivedHandler;
    protected enum RecivedState {
      /// <summary>
      /// receiving
      /// </summary>
      Recving = 0,
      /// <summary>
      /// Task Cancelled
      /// </summary>
      Cancelled = -4,
      /// <summary>
      /// Recev timed out
      /// </summary>
      TimedOut = -3,
      /// <summary>
      /// this package didnt pass CRC verify
      /// </summary>
      CRCFail = -2,
      /// <summary>
      /// received a nak package
      /// </summary>
      NAK = -1,
      /// <summary>
      /// received a ack package
      /// </summary>
      ACK = 1,
      /// <summary>
      /// received a data package
      /// </summary>
      Data = 2,
    }
    public void Dispose() {

      try { _SerialPort.Dispose(); } catch { }
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
  }

  public enum CommandMark : byte {
    Reset = 0x30,
    GetStatus = 0x31,
    SetSecurity = 0x32,
    Poll = 0x33,
    EnableBillTypes = 0x34,
    Stack = 0x35,
    Return = 0x36,
    Identification = 0x37,
    Hold = 0x38,
    SetBarcodeParas = 0x39,
    ExtractBarcodeData = 0x3A,
    RecyclingCassetteStatus = 0x3B,
    Dispense = 0x3C,
    Unload = 0x3D,
    ExtendedIdentification = 0x3E,
    SetRecyclingCassetteType = 0x40,
    GetBillTable = 0x43,
    GetCapacityLimitOfCassette = 0x44,
    Download = 0x50,
    GetCRC32OfTheCode = 0x51,
    ModuleDownload = 0x52,
    ModuleIdentificationRequest = 0x53,
    ValidationModuleIdentification = 0x54,
    GetCRC16OfTheCode = 0x56,
    RequestStatistics = 0x60,
    RequestOrSetDateTime = 0x62,
    PowerRecovery = 0x66,
    EmptyDispenser = 0x67,
    SetOptions = 0x68,
    GetOptions = 0x69,
    ExtendedCassetteStatus = 0x70,
    DiagnosticOrSetting = 0xF0,
  }
  /// <summary>
  /// Code|SubCode
  /// </summary>
  public enum PollRecivedPackageType {
    /// <summary>
    /// 上电
    /// </summary>
    PowerUp = 0x1000 | 0x0000,
    /// <summary>
    /// 上电，有纸币在识别头
    /// </summary>
    PowerUpWithBillInValidator = 0x1100 | 0x0000,
    /// <summary>
    /// 上电，有纸币在钱箱
    /// </summary>
    PowerUpWithBillInChassis = 0x1200 | 0x0000,
    Initialize = 0x1300 | 0x0000,
    /// <summary>
    /// 空闲，已准备好接受放入纸钞
    /// </summary>
    Idling = 0x1400 | 0x0000,
    /// <summary>
    /// 有新纸钞放入
    /// </summary>
    Accepting = 0x1500 | 0x0000,
    /// <summary>
    /// 正在压钞
    /// </summary>
    Stacking = 0x1700 | 0x0000,
    Returning = 0x1800 | 0x0000,
    /// <summary>
    /// 禁止收币
    /// </summary>
    Disabled = 0x1900 | 0x0000,
    /// <summary>
    /// 保持
    /// </summary>
    Holding = 0x1A00 | 0x0000,
    Busy = 0x1B00 | 0x0000,


    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Insertion = 0x1C00 | 0x0060,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Magnetic = 0x1C00 | 0x0061,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Bill = 0x1C00 | 0x0062,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Multiply = 0x1C00 | 0x0063,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Conveying = 0x1C00 | 0x0064,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Identification = 0x1C00 | 0x0065,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Verification = 0x1C00 | 0x0066,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Optic = 0x1C00 | 0x0067,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Inhibit = 0x1C00 | 0x0068,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Capacity = 0x1C00 | 0x0069,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Operation = 0x1C00 | 0x006A,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Length = 0x1C00 | 0x006C,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_UV = 0x1C00 | 0x006D,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Barcode_Unrecognized = 0x1C00 | 0x0092,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Barcode_IncorrectNum = 0x1C00 | 0x0093,
    /// <summary>
    /// 不识别
    /// </summary>
    Rejected_Barcode_UnknownStart = 0x1C00 | 0x0094,
    /// <summary>
    /// 不识别
    /// </summary>
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

    /// <summary>
    /// 钱箱满
    /// </summary>
    DropCassetteFull = 0x4100 | 0x0000,
    /// <summary>
    /// 钱箱已移除
    /// </summary>
    DropCassetteRemoved = 0x4200 | 0x0000,

    /// <summary>
    /// 接受口卡币
    /// </summary>
    JammedInAcceptor = 0x4300 | 0x0000,
    /// <summary>
    /// 钱箱卡币
    /// </summary>
    JammedInStacker = 0x4400 | 0x0000,
    /// <summary>
    /// 欺骗行为
    /// </summary>
    Cheated = 0x4500 | 0x0000,
    /// <summary>
    /// 通用故障码
    /// </summary>
    GenericErrorCode = 0x4700 | 0x0000,
    /// <summary>
    /// 暂存纸币
    /// SubCode:BillType
    /// </summary>
    ESCROW = 0x8000 | 0x0000,
    /// <summary>
    /// 已压钞
    /// SubCode:BillType
    /// Data:L=1B
    /// </summary>
    PackedOrStacked = 0x8100 | 0x0000,
    /// <summary>
    /// 纸币被退回
    /// </summary>
    Returned = 0x8200 | 0x0000,

    WaittingOfDecision = 0x8300 | 0x0100,
  }
  public struct Command {
    /// <summary>
    /// full command data,from SYN to CRC
    /// </summary>
    public byte[] CommandData { get; set; }
    /// <summary>
    /// This is reference of readed received buffer,reorgnized by SYN flag,SYN always on Index0,next communicate loop will rewrite this array.
    /// if need save data,should copy form this array
    /// </summary>
    public byte[] ResponseData { get; set; }
    /// <summary>
    /// how long readed
    /// </summary>
    public int ResponsDataLength { get; set; }
    /// <summary>
    /// got a ack package responsed from device
    /// </summary>
    public bool IsACKResponsed { get; set; }
    public CommandMark? CommandMark { get; set; }
    public PollRecivedPackageType? ResponseMark {
      get {
        if (CommandMark.HasValue && CommandMark.Value == CashCodeProtocol.CommandMark.Poll) {
          if (ResponseData[2] - 2 == 4) {
            return (PollRecivedPackageType)((ResponseData[3] << 8) | 0x0000);
          }
          return (PollRecivedPackageType)(((ResponseData[3] << 8) & 0xFFFF) | (ResponseData[4]) & 0xFFFF);
        }
        return null;
      }
    }
    public byte? SubResponseMark {
      get {
        if (CommandMark.HasValue && CommandMark.Value == CashCodeProtocol.CommandMark.Poll) {
          if (ResponseData[2] - 2 == 4) {
            return null;
          }
          return (byte)((ResponseData[4]) & 0xFF);
        }
        return null;
      }
    }

  }

  public class CashCodeB2BCfg {
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