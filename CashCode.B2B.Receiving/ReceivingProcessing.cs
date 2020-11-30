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
      //Device.OnRecivedHandler += Device_OnRecivedHandler;
    }

    /*
     
   Enable -> Idling -> Accepting -> if(Error:Rej) -> Rej -> Idling -> ...(Waitting Disable)
                                 -> if(Ok)        -> Stacked/Packed -> Idling -> ...(Waitting Disable)
                                 
                                 -> if(Error:(other error)) -> !Stop

     */

    private void Device_OnRecivedHandler(Command Packet) {
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
          break;
        case PollRecivedPackageType.Dispensed:
          break;
        case PollRecivedPackageType.Unloaded:
          break;
        case PollRecivedPackageType.InvalidBillNumber:
          break;
        case PollRecivedPackageType.SettedTypeCassette:
          break;
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
        case PollRecivedPackageType.WaittingOfDecision:
          break;
        case PollRecivedPackageType.PowerUp:
        case PollRecivedPackageType.PowerUpWithBillInValidator:
        case PollRecivedPackageType.PowerUpWithBillInChassis:
        case PollRecivedPackageType.Initialize:

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
