namespace CashCode.B2B.Receiving {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  using CashCodeProtocol;

  using Microsoft.Extensions.Logging;

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
    Rej         0x1C__  30
    Disabled    0x1900  40
    Stacked     0x81__  30
    Error       0x4___  40

     */

    private PollRecivedPackageType _LastType;
    private void Device_OnRecivedHandler(Command Packet) {
      if (Packet.ResponseMark.HasValue && _LastType != Packet.ResponseMark.Value) {
        _LastType = Packet.ResponseMark.Value;



      }
      else return;
      switch (Packet.ResponseMark) {
        case PollRecivedPackageType.Idling:

          break;
        case PollRecivedPackageType.Accepting:

          break;
        case PollRecivedPackageType.Stacking:
          break;
        case PollRecivedPackageType.Returning:
          break;
        case PollRecivedPackageType.Disabled:
          break;
        case PollRecivedPackageType.Holding:
          break;
        case PollRecivedPackageType.ESCROW:
          break;
        case PollRecivedPackageType.PackedOrStacked:
          break;
        case PollRecivedPackageType.Returned:
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
          break;
        case PollRecivedPackageType.Dispensing_Recycling2Dispenser:
        case PollRecivedPackageType.Dispensing_WaittingCustomeTake:
          break;
        case PollRecivedPackageType.Unloading_Recycling2Drop:
        case PollRecivedPackageType.Unloading_Recycling2Drop_TooMuchBills:
          break;
        case PollRecivedPackageType.SettingTypeCassette:
        case PollRecivedPackageType.Dispensed:
        case PollRecivedPackageType.Unloaded:
        case PollRecivedPackageType.InvalidBillNumber:
        case PollRecivedPackageType.SettedTypeCassette:
        case PollRecivedPackageType.InvalidCommand:
          break;
        case PollRecivedPackageType.DropCassetteFull:
          break;
        case PollRecivedPackageType.DropCassetteRemoved:
          break;
        case PollRecivedPackageType.JammedInAcceptor:
        case PollRecivedPackageType.JammedInStacker:
          break;
        case PollRecivedPackageType.Cheated:

          break;
        case PollRecivedPackageType.GenericErrorCode:

          break;
        case PollRecivedPackageType.PowerUpWithBillInValidator:
        case PollRecivedPackageType.PowerUpWithBillInChassis:

          break;
        case PollRecivedPackageType.Initialize:

          break;
        case PollRecivedPackageType.WaittingOfDecision:
        case PollRecivedPackageType.PowerUp:

        default:
          break;
      }
    }

    protected readonly Dictionary<byte, int> _ValueMap;
    protected readonly Stack<IRecevingStep> _StepStack = new Stack<IRecevingStep>();

    public int TotalValue { get; protected set; }

    public event Action OnCassetteFullHandler;
    public event Action OnCassetteLoseHandler;
    public event Action OnJamedHandler;
    public event Action OnGenericErrorCatchedHandler;
    public event Action OnCheatedHandler;
    public event Action OnRejectHandler;
    public event Action OnBusyHandler;
    public event Action OnPackedOrStackedHandler;
    public event Action OnIdlingHandler;
    public event Action OnAcceptingHandler;
    public event Action OnStackingHandler;
    public event Action OnInitializeHandler;

  }

  public interface IRecevingStep {
    int Step { get; }
    PollRecivedPackageType PollResponsed { get; }
    byte[] Data { get; }

    void OnEvent();
    void OnEject();
  }

  public struct PollInitialize : IRecevingStep {
    public int Step { get; }
    public PollRecivedPackageType PollResponsed { get; }
    public byte[] Data { get; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }
  public struct PollIdling : IRecevingStep {
    public int Step { get; }
    public PollRecivedPackageType PollResponsed { get; }
    public byte[] Data { get; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }
  public struct PollAccepting : IRecevingStep {
    public int Step { get; }
    public PollRecivedPackageType PollResponsed { get; }
    public byte[] Data { get; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }
  public struct PollRejected : IRecevingStep {
    public int Step { get; }
    public PollRecivedPackageType PollResponsed { get; }
    public byte[] Data { get; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }
  public struct PollDisabled : IRecevingStep {
    public int Step { get; }
    public PollRecivedPackageType PollResponsed { get; }
    public byte[] Data { get; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }
  public struct PollError : IRecevingStep {
    public int Step { get; set; }
    public PollRecivedPackageType PollResponsed { get; set; }
    public byte[] Data { get; set; }

    public void OnEvent() {

    }

    public void OnEject() {

    }
  }

}
