namespace CashCode.B2B.Receiving {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  using CashCodeProtocol;

  using Microsoft.Extensions.Logging;

  using static CashCode.B2B.Receiving.ObejctStack;

  public class B2BReceivingProcessing : CashCodeB2B {
    public B2BReceivingProcessing(CashCodeB2BCfg Cfg, Dictionary<byte, int> ValueMap, ILogger<CashCodeB2B> Logger) : base(Cfg, Logger) {
      _ValueMap = ValueMap;
      this.OnRecivedHandler += Device_OnRecivedHandler;
    }

    /*
     
   Initialize -> Idling -> Accepting -> if(Error:Rej) -> Rej -> Disabled -> Idling -> ...(Waitting Disable)  ✓
                                     -> if(Ok)        -> Stacked/Packed -> Idling -> ...(Waitting Disable)
                                 
                                     -> if(Error:(other error)) -> !Stop



    Initialize  0x1300  0

    Idling      0x1400  10
    Accepting   0x1500  20
    Rej         0x1C__  30
    Disabled    0x1900  40
    Stacked     0x81__  30

    Error       0x4___  0

     */

    private PollRecivedPackageType _LastType;
    private void Device_OnRecivedHandler(Command Packet) {
      if (Packet.ResponseMark.HasValue && _LastType != Packet.ResponseMark.Value) {
        _LastType = Packet.ResponseMark.Value;



      }
      else return;
      switch (_LastType) {
        case PollRecivedPackageType.Idling:
          _StepStack.Push(new PollIdling() { Step = 10, PollResponsed = PollRecivedPackageType.Idling, Data = null });
          break;
        case PollRecivedPackageType.Accepting:
          _StepStack.Push(new PollAccepting() { Step = 20, PollResponsed = PollRecivedPackageType.Accepting, Data = null });
          break;
        case PollRecivedPackageType.Stacking:
          break;
        case PollRecivedPackageType.Returning:
          break;
        case PollRecivedPackageType.Disabled:
          _StepStack.Push(new PollDisabled() { Step = 40, PollResponsed = PollRecivedPackageType.Disabled, Data = null });
          break;
        case PollRecivedPackageType.Holding:
          break;
        case PollRecivedPackageType.ESCROW:
          break;
        case PollRecivedPackageType.PackedOrStacked:
          var St1 = new PollStockedPacked { Step = 30, PollResponsed = PollRecivedPackageType.PackedOrStacked, Data = new byte[Packet.ResponsDataLength] };
          Array.Copy(Packet.ResponseData, 0, St1.Data, 0, Packet.ResponsDataLength);
          _StepStack.Push(St1);
          break;
        case PollRecivedPackageType.Returned:
          _StepStack.Push(new PollReturned() { Step = 30, PollResponsed = PollRecivedPackageType.Returned, Data = null });
          break;
        case PollRecivedPackageType.Busy:
          break;
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
          _StepStack.Push(new PollRejected() { Step = 30, PollResponsed = _LastType, Data = null });
          break;
        case PollRecivedPackageType.Dispensing_Recycling2Dispenser:
        case PollRecivedPackageType.Dispensing_WaittingCustomeTake:
          break;
        case PollRecivedPackageType.Unloading_Recycling2Drop:
        case PollRecivedPackageType.Unloading_Recycling2Drop_TooMuchBills:
          break;
        case PollRecivedPackageType.SettingTypeCassette:
        case PollRecivedPackageType.SettedTypeCassette:
          break;
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
          var St2 = new PollError { Step = 0, PollResponsed = _LastType, Data = new byte[Packet.ResponsDataLength] };
          Array.Copy(Packet.ResponseData, 0, St2.Data, 0, Packet.ResponsDataLength);
          _StepStack.Push(St2);
          break;
        case PollRecivedPackageType.Initialize:
          _StepStack.Push(new PollInitialize { Step = 0, PollResponsed = PollRecivedPackageType.Initialize, Data = null });
          break;
        case PollRecivedPackageType.WaittingOfDecision:
        case PollRecivedPackageType.PowerUp:
        default:
          break;
      }
    }

    protected readonly Dictionary<byte, int> _ValueMap;
    private readonly ObejctStack _StepStack = new ObejctStack(null, null, 10);

    public int TotalValue { get; protected set; }

    /// <summary>
    /// 禁止放入纸币
    /// </summary>
    public void DisableReveving() => base.SendEnableBillTypes(0x00);
    /// <summary>
    /// 允许放入纸币
    /// </summary>
    public void EnableReceving() => base.SendEnableBillTypes(_Cfg.EnableCashType);


    /// <summary>
    /// 发生必须人工干预的错误
    /// </summary>
    public event Action<B2BReceivingProcessing> OnErrorCatchedHandler;
    /// <summary>
    /// 纸币不被识别
    /// </summary>
    public event Action<B2BReceivingProcessing> OnRejectHandler;
    /// <summary>
    /// 正在处理上一张，暂停收币
    /// </summary>
    public event Action<B2BReceivingProcessing> OnDisabledHandler;
    /// <summary>
    /// 纸币被退回
    /// </summary>
    public event Action<B2BReceivingProcessing> OnReturnedHandler;
    /// <summary>
    /// 已收币，正在压入钱箱
    /// </summary>
    public event Action<B2BReceivingProcessing> OnPackedOrStackedHandler;
    /// <summary>
    /// 已准备好接受下一张
    /// </summary>
    public event Action<B2BReceivingProcessing> OnIdlingHandler;
    /// <summary>
    /// 检查到纸币放入
    /// </summary>
    public event Action<B2BReceivingProcessing> OnAcceptingHandler;
    /// <summary>
    /// 已完成初始化
    /// </summary>
    public event Action<B2BReceivingProcessing> OnInitializeHandler;

    internal struct PollInitialize : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnEvent() { }
      public void OnEject() { }
    }
    internal struct PollIdling : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnIdlingHandler?.Invoke(Device);
      public void OnEject() { }
    }
    internal struct PollAccepting : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnAcceptingHandler?.Invoke(Device);
      public void OnEject() { }
    }
    internal struct PollStockedPacked : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnEvent() {
        if (Device._StepStack.Count > 0) {
          var LastStep = Device._StepStack.Peek();
          //向前检查
          if (LastStep.PollResponsed == PollRecivedPackageType.Accepting || LastStep.PollResponsed == PollRecivedPackageType.Stacking) {

            if (Sub.HasValue && Device._ValueMap.TryGetValue(Sub.Value, out var Val)) {
              Device.TotalValue += Val;
            }
            else {
              //TODO No BillType Or Type Not in Map
            }

            Device.OnPackedOrStackedHandler?.Invoke(Device);
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
      public void OnEject() { }
    }
    internal struct PollRejected : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnRejectHandler?.Invoke(Device);
      public void OnEject() { }
    }
    internal struct PollDisabled : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnDisabledHandler?.Invoke(Device);
      public void OnEject() { }
    }
    internal struct PollReturned : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[4]; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnReturnedHandler?.Invoke(Device);
      public void OnEject() { }
    }
    internal struct PollError : IRecevingStep {
      public B2BReceivingProcessing Device { get; set; }
      public int Step { get; set; }
      public PollRecivedPackageType PollResponsed { get; set; }
      public byte? Sub { get => Data[2] - 2 - 3 - 1 > 0 ? Data[4] : (byte)0x00; }
      public byte[] Data { get; set; }
      public void OnEvent() => Device.OnErrorCatchedHandler?.Invoke(Device);
      public void OnEject() { }
    }
    //internal struct SkipStep : IRecevingStep {
    //  public B2BReceivingProcessing Device { get; set; }
    //  public int Step { get; set; }
    //  public PollRecivedPackageType PollResponsed { get; set; }
    //  public byte? Sub { get => Data[4]; }
    //  public byte[] Data { get; set; }
    //  public void OnEvent() => Device.OnReturnedHandler?.Invoke(Device);
    //  public void OnEject() { }
    //}
  }
  internal interface IRecevingStep {
    B2BReceivingProcessing Device { get; }
    int Step { get; }
    PollRecivedPackageType PollResponsed { get; }
    byte[] Data { get; }
    void OnEvent();
    void OnEject();
  }


  internal sealed class ObejctStack : Stack<IRecevingStep> {
    public ObejctStack(Action<IRecevingStep> OnPush, Action<IRecevingStep> OnPop) : this(OnPush, OnPop, 512) { }
    public ObejctStack(Action<IRecevingStep> OnPush, Action<IRecevingStep> OnPop, int Capacity) : base(Capacity) {
      this.OnPop = OnPop;
      this.OnPush = OnPush;
    }

    private readonly Action<IRecevingStep> OnPush;
    private readonly Action<IRecevingStep> OnPop;

    public new void Push(IRecevingStep Item) {
      if (Count == 0) {
        Item.OnEvent();
        base.Push(Item);
        OnPush?.Invoke(Item);
      }
      else if (Item.Step <= base.Peek().Step) {
        base.Peek().OnEject();
        OnPop?.Invoke(base.Pop());
      }
      else {
        Item.OnEvent();
        base.Push(Item);
        OnPush?.Invoke(Item);
      }
    }
    public new IRecevingStep Pop() {
      if (Count > 0) {
        base.Peek().OnEject();
        var Item = base.Pop();
        OnPop?.Invoke(Item);
        return Item;
      }
      return default;
    }
    public new void Clear() {
      while (Count > 0) {
        this.Pop();
      }
    }

  }



}
