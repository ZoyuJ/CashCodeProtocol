namespace CashCode.B2B.Receiving {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  using CashCodeProtocol;

  class B2BReceivingProcessing {
    public B2BReceivingProcessing(CashCodeB2B Device, Dictionary<byte, int> ValueMap) {
      _ValueMap = ValueMap;
      Device.OnRecivedHandler += Device_OnRecivedHandler;
    }

    /*
     
   Enable -> Idling -> Accepting -> if(Error:Rej) -> Rej -> Idling -> ...(Waitting Disable)
                                 -> if(Ok)        -> Stacked/Packed -> Idling -> ...(Waitting Disable)
                                 
                                 -> if(Error:(other error)) -> !Stop

     */

    private void Device_OnRecivedHandler(CashCodeB2B.PollRecivedPackageType MainType, byte SubTypeType, byte[] arg3) {
      switch (MainType) {
        case CashCodeB2B.PollRecivedPackageType.Idling:
          
          break;
        case CashCodeB2B.PollRecivedPackageType.Accepting:

          break;
        case CashCodeB2B.PollRecivedPackageType.Stacking:
          break;
        case CashCodeB2B.PollRecivedPackageType.Returning:
          break;
        case CashCodeB2B.PollRecivedPackageType.Disabled:
          break;
        case CashCodeB2B.PollRecivedPackageType.Holding:
          break;
        case CashCodeB2B.PollRecivedPackageType.ESCROW:
          break;
        case CashCodeB2B.PollRecivedPackageType.PackedOrStacked:
          break;
        case CashCodeB2B.PollRecivedPackageType.Returned:
          break;
        case CashCodeB2B.PollRecivedPackageType.Busy:
          break;
        case CashCodeB2B.PollRecivedPackageType.Rejected_Insertion:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Magnetic:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Bill:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Multiply:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Conveying:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Identification:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Verification:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Optic:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Inhibit:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Capacity:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Operation:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Length:
        case CashCodeB2B.PollRecivedPackageType.Rejected_UV:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Barcode_Unrecognized:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Barcode_IncorrectNum:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Barcode_UnknownStart:
        case CashCodeB2B.PollRecivedPackageType.Rejected_Barcode_UnknownStop:
          break;
        case CashCodeB2B.PollRecivedPackageType.Dispensing_Recycling2Dispenser:
        case CashCodeB2B.PollRecivedPackageType.Dispensing_WaittingCustomeTake:
          break;
        case CashCodeB2B.PollRecivedPackageType.Unloading_Recycling2Drop:
        case CashCodeB2B.PollRecivedPackageType.Unloading_Recycling2Drop_TooMuchBills:
          break;
        case CashCodeB2B.PollRecivedPackageType.SettingTypeCassette:
          break;
        case CashCodeB2B.PollRecivedPackageType.Dispensed:
          break;
        case CashCodeB2B.PollRecivedPackageType.Unloaded:
          break;
        case CashCodeB2B.PollRecivedPackageType.InvalidBillNumber:
          break;
        case CashCodeB2B.PollRecivedPackageType.SettedTypeCassette:
          break;
        case CashCodeB2B.PollRecivedPackageType.InvalidCommand:
          break;
        case CashCodeB2B.PollRecivedPackageType.DropCassetteFull:
          break;
        case CashCodeB2B.PollRecivedPackageType.DropCassetteRemoved:
          break;
        case CashCodeB2B.PollRecivedPackageType.JammedInAcceptor:
        case CashCodeB2B.PollRecivedPackageType.JammedInStacker:
          break;
        case CashCodeB2B.PollRecivedPackageType.Cheated:
          break;
        case CashCodeB2B.PollRecivedPackageType.GenericErrorCode:
          break;
        case CashCodeB2B.PollRecivedPackageType.WaittingOfDecision:
          break;
        case CashCodeB2B.PollRecivedPackageType.PowerUp:
        case CashCodeB2B.PollRecivedPackageType.PowerUpWithBillInValidator:
        case CashCodeB2B.PollRecivedPackageType.PowerUpWithBillInChassis:
        case CashCodeB2B.PollRecivedPackageType.Initialize:
     
        default:
          break;
      }
    }

    protected readonly Dictionary<byte, int> _ValueMap;
    protected readonly Stack<IRecevingStep> _StepStack = new Stack<IRecevingStep>();





  }

  interface IRecevingStep {
    int Step { get; }
  }

}
