namespace CashCodeProtocol.B2B.Receiving {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Text;

  using CashCodeProtocol.B2B;

  using CommandNavigation;
  using CommandNavigation.Command1Navigation2;

  using Microsoft.Extensions.Logging;


  public class B2BReceivingProcessing : CashCodeB2B, IEnumerable<int> {
    public static readonly int[] BillTypes_CNY = new int[] { 1, 2, 5, 10, 20, 50, 100 };
    public B2BReceivingProcessing(CashCodeB2BCfg Cfg, ILogger<CashCodeB2B> Logger) : base(Cfg, Logger) {
      this.OnReceived += Device_OnRecivedHandler;
    }

    /*
     #NO ESCROW
      0             10        20     ┃                    30      10          10
   Initialize -> Idling -> Accepting ┃-> if(Error:Rej) -> Rej -> Disabled -> Idling -> ...(Waitting Disable)  ✓
                                     ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                                     ┃                      40                10  
                                     ┃-> if(Ok)        -> Stacked/Packed -> Idling -> ...(Waitting Disable)   ✓
                                     ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                                     ┃                     0
                                     ┃-> if(Error:(other error)) -> !Stop                                     ✓


    # ESCROW
      0           10          20     ┃                    30      10          10
   Initialize -> Idling -> Accepting ┃-> if(Error:Rej) -> Rej -> Disabled -> Idling -> ...(Waitting Disable)                       ✓ 
                                     ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                                     ┃               30                          40            50        10
                                     ┃-> if(Ok)  -> ESCROW -> if(Return)   -> Returning -> Returned -> Disabled                    ✓
                                     ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                                     ┃                                           40              10
                                     ┃                     -> if(Stack)    -> Stacked/Packed -> Idling -> ...(Waitting Disable)    ✓
                                     ┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                                     ┃                                 0
                                     ┃-> if(Error:(other error)) -> !Stop                                                          ✓

     */

    private PollRecivedPackageType _LastType;
    private StringBuilder ExportCurrentStepStack() {
      StringBuilder SB = new StringBuilder("\tStart Step Stack\n");
      foreach (var item in _StepStack) {
        SB.Append("\t\t");
        SB.AppendLine(item.ToString());
      }
      SB.Append("\t\tE n d Step Stack");
      return SB;
    }
    private void Device_OnRecivedHandler(Command Packet) {
      Console.WriteLine(Packet.ResponseMark);
      if (Packet.ResponseMark.HasValue && _LastType != Packet.ResponseMark.Value) {
        _LastType = Packet.ResponseMark.Value;
      }
      else return;
      switch (_LastType) {
        #region Error
        case PollRecivedPackageType.Dispensed:
        case PollRecivedPackageType.Unloaded:
        case PollRecivedPackageType.InvalidBillNumber:
        case PollRecivedPackageType.InvalidCommand:
        case PollRecivedPackageType.DropCassetteFull:
        case PollRecivedPackageType.DropCassetteRemoved:
        case PollRecivedPackageType.JammedInAcceptor:
        case PollRecivedPackageType.JammedInStacker:
        case PollRecivedPackageType.Cheated:
        case PollRecivedPackageType.GenericErrorCode:
        case PollRecivedPackageType.PowerUpWithBillInValidator:
        case PollRecivedPackageType.PowerUpWithBillInChassis:
          var St2 = new PollError { Device = this, Order = 0, PollResponsed = _LastType, Data = new byte[Packet.ResponsDataLength] };
          Array.Copy(Packet.ResponseData, 0, St2.Data, 0, Packet.ResponsDataLength);
          _StepStack.Push(St2);
          _Logger?.LogError(ExportCurrentStepStack().ToString());
          break;
        #endregion
        case PollRecivedPackageType.Initialize:
          _StepStack.Push(new PollInitialize { Device = this, Order = 0, PollResponsed = PollRecivedPackageType.Initialize, Data = null });
          break;
        case PollRecivedPackageType.Disabled:
          _StepStack.Push(new PollDisabled() { Device = this, Order = 10, PollResponsed = PollRecivedPackageType.Disabled, Data = null });
          break;
        case PollRecivedPackageType.Idling:
          _StepStack.Push(new PollIdling() { Device = this, Order = 10, PollResponsed = PollRecivedPackageType.Idling, Data = null });
          break;
        case PollRecivedPackageType.Accepting:
          _StepStack.Push(new PollAccepting() { Device = this, Order = 20, PollResponsed = PollRecivedPackageType.Accepting, Data = null });
          break;
        #region Rej
        case PollRecivedPackageType.Rejected_Insertion:
        case PollRecivedPackageType.Rejected_Magnetic:
        case PollRecivedPackageType.Rejected_Bill:
        case PollRecivedPackageType.Rejected_Multiply:
        case PollRecivedPackageType.Rejected_Conveying:
        case PollRecivedPackageType.Rejected_Identification:
        case PollRecivedPackageType.Rejected_Verification:
        case PollRecivedPackageType.Rejected_Optic:
        case PollRecivedPackageType.Rejected_Inhibit:
        case PollRecivedPackageType.Rejected_Capacity:
        case PollRecivedPackageType.Rejected_Operation:
        case PollRecivedPackageType.Rejected_Length:
        case PollRecivedPackageType.Rejected_UV:
        case PollRecivedPackageType.Rejected_Barcode_Unrecognized:
        case PollRecivedPackageType.Rejected_Barcode_IncorrectNum:
        case PollRecivedPackageType.Rejected_Barcode_UnknownStart:
        case PollRecivedPackageType.Rejected_Barcode_UnknownStop:
          _StepStack.Push(new PollRejected() { Device = this, Order = 30, PollResponsed = _LastType, Data = null });
          break;
        #endregion
        case PollRecivedPackageType.ESCROW:
          _StepStack.Push(new PollESCROW() { Device = this, Order = 30, PollResponsed = PollRecivedPackageType.ESCROW, Data = null });
          break;
        case PollRecivedPackageType.Stacking:
          _StepStack.Push(new PollStacking() { Device = this, Order = 35, PollResponsed = PollRecivedPackageType.Stacking, Data = null });
          break;
        case PollRecivedPackageType.PackedOrStacked:
          var St1 = new PollStockedPacked { Device = this, Order = 40, PollResponsed = PollRecivedPackageType.PackedOrStacked, Data = new byte[Packet.ResponsDataLength] };
          Array.Copy(Packet.ResponseData, 0, St1.Data, 0, Packet.ResponsDataLength);
          _StepStack.Push(St1);
          break;
        case PollRecivedPackageType.Returning:
          _StepStack.Push(new PollReturnning() { Device = this, Order = 40, PollResponsed = PollRecivedPackageType.Returning, Data = null });
          break;
        case PollRecivedPackageType.Returned:
          _StepStack.Push(new PollReturned() { Device = this, Order = 50, PollResponsed = PollRecivedPackageType.Returned, Data = null });
          break;
        #region Ignore
        case PollRecivedPackageType.Holding:

        case PollRecivedPackageType.Busy:

        case PollRecivedPackageType.Dispensing_Recycling2Dispenser:
        case PollRecivedPackageType.Dispensing_WaittingCustomeTake:

        case PollRecivedPackageType.Unloading_Recycling2Drop:
        case PollRecivedPackageType.Unloading_Recycling2Drop_TooMuchBills:

        case PollRecivedPackageType.SettingTypeCassette:
        case PollRecivedPackageType.SettedTypeCassette:

        case PollRecivedPackageType.WaittingOfDecision:
        case PollRecivedPackageType.PowerUp:
        default:
          break;
          #endregion
      }
    }

    internal readonly CommandNavigation<IRecevingStep> _StepStack = new CommandNavigation<IRecevingStep>(10);

    public int TotalValue { get=>_ReceivedCash.Sum();  }
    public int Count { get => _ReceivedCash.Count; }
    protected readonly Stack<int> _ReceivedCash = new Stack<int>();

    /// <summary>
    /// 禁止放入纸币
    /// </summary>
    public void DisableReveving() => base.SendEnableBillTypes(0x00, 0x00);
    /// <summary>
    /// 允许放入纸币
    /// </summary>
    public void EnableReceving() {
      base.SendEnableBillTypes(_Cfg.EnableCashType, _Cfg.EnableEscrowType);
      _ReceivedCash.Clear();
    }


    /// <summary>
    /// 发生必须人工干预的错误
    /// </summary>
    public event OnErrorCatchedEventHandler OnErrorCatched;
    /// <summary>
    /// 纸币不被识别
    /// </summary>
    public event OnRejectedEventHandler OnRejected;
    /// <summary>
    /// 正在处理上一张，暂停收币
    /// </summary>
    public event OnDisabledEventHandler OnDisabled;
    /// <summary>
    /// 纸币被退回
    /// </summary>
    public event OnReturnedEventHandler OnReturned;
    public event OnReturningEventHandler OnReturning;
    /// <summary>
    /// 已收币，正在压入钱箱
    /// </summary>
    public event OnPackedOrStackedEventHandler OnPackedOrStacked;
    /// <summary>
    /// 已准备好接受下一张
    /// </summary>
    public event OnIdlingEventHandler OnIdling;
    /// <summary>
    /// 检查到纸币放入
    /// </summary>
    public event OnAcceptingEventHandler OnAccepting;
    public event OnESCROWEventHandler OnESCROW;
    public event OnStackingEventHandler OnStacking;
    /// <summary>
    /// 已完成初始化
    /// </summary>
    public event OnInitializeEventHandler OnInitialize;

    internal struct PollInitialize : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() {
        Device._StepStack.Discard();
        Device._ReceivedCash.Clear();
        Device.OnInitialize?.Invoke(Device, this);
      }
      public void OnPop() { }


      public override string ToString() {
        return $"Initialize,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollIdling : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnIdling?.Invoke(Device, this);
      public void OnPop() { }


      public override string ToString() {
        return $"Idling,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollAccepting : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnAccepting?.Invoke(Device, this);
      public void OnPop() { }


      public override string ToString() {
        return $"Accepting,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollESCROW : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnESCROW?.Invoke(Device, this, Sub.Value);
      public void OnPop() { }


      public override string ToString() {
        return $"Accepting,Order{Order},ResponsedType{PollResponsed},SubData{Sub},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollStacking : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnStacking?.Invoke(Device, this);
      public void OnPop() { }


      public override string ToString() {
        return $"Stacking,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollStockedPacked : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnPush() {
        if (Device._StepStack.Count > 0) {
          var LastStep = Device._StepStack.Peek();
          //向前检查
          if (LastStep.PollResponsed == PollRecivedPackageType.Accepting || LastStep.PollResponsed == PollRecivedPackageType.Stacking) {
            if (Sub.HasValue) {
              var Val = BillTypes_CNY[Sub.Value];
              //Device.TotalValue += Val;
              Device._ReceivedCash.Push(Val);
            }
            else {
              Debug.WriteLine("Unknown Cash Value Type");
              //TODO No BillType Or Type Not in Map
            }
            Device.OnPackedOrStacked?.Invoke(Device, this, Sub.Value);
            return;
          }
        }
        //TODO 向后检查与无来源收币
        /*
                           在确认面额的数据0x81 之后要看之前的POLL 返回数据是否是0x15 或者0x17，
          向前检查？-->    如果不是，说明该纸币不是正常的收钞过程，可能是之前的压钞不成功或者卡币的纸币，所以需要专门记录，必要的话提示需要人工干预；
          向后检查？-->    还要看之后的POLL 返回数据是否是0x14 或者0x19.
         */

      }
      public void OnPop() { }

      public override string ToString() {
        return $"StockedPacked,Order{Order},ResponsedType{PollResponsed},SubData{Sub},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollRejected : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnRejected?.Invoke(Device, this);
      public void OnPop() { }

      public override string ToString() {
        return $"Rejected,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollDisabled : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnDisabled?.Invoke(Device, this);
      public void OnPop() { }


      public override string ToString() {
        return $"Disabled,Order{Order},ResponsedType{PollResponsed},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollReturned : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnPush() {
        Device.OnReturned?.Invoke(Device, this, Sub.Value);
      }
      public void OnPop() { }


      public override string ToString() {
        return $"Returned,Order{Order},ResponsedType{PollResponsed},SubData{Sub},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollReturnning : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnReturning?.Invoke(Device, this);
      public void OnPop() { }


      public override string ToString() {
        return $"Returned,Order{Order},ResponsedType{PollResponsed},SubData{Sub},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }
    internal struct PollError : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Order { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[2] - 2 - 3 - 1 > 0 ? Data[4] : (byte)0x00; }
      public byte[] Data { get; set; }
      public void OnPush() => Device.OnErrorCatched?.Invoke(Device, this, Sub.Value);
      public void OnPop() { }


      public override string ToString() {
        return $"Error,Order{Order},ResponsedType{PollResponsed},SubData{Sub},Data{BitConverter.ToString(Data ?? new byte[0])}";
      }
    }


    public IEnumerator<int> GetEnumerator() {
      return _ReceivedCash.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return _ReceivedCash.GetEnumerator();
    }


#if DEBUG
    //public string ToDebugCommandStackString() {

    //}
#endif

  }
  public interface IRecevingStep : ICommandCtrl {
    B2BReceivingProcessing Device { get; }
    PollRecivedPackageType PollResponsed { get; }
    byte[] Data { get; }
  }

  public delegate void OnInitializeEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnAcceptingEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnESCROWEventHandler(B2BReceivingProcessing Device, IRecevingStep Data, byte Sub);
  public delegate void OnStackingEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnIdlingEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnPackedOrStackedEventHandler(B2BReceivingProcessing Device, IRecevingStep Data, byte Sub);
  public delegate void OnReturnedEventHandler(B2BReceivingProcessing Device, IRecevingStep Data, byte Sub);
  public delegate void OnReturningEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnDisabledEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnRejectedEventHandler(B2BReceivingProcessing Device, IRecevingStep Data);
  public delegate void OnErrorCatchedEventHandler(B2BReceivingProcessing Device, IRecevingStep Data, byte Sub);

}
