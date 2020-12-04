namespace Try {
  using System;
  using System.Threading.Tasks;

  using CashCodeProtocol.B2B.Receiving;

  class Program {
    static void Main(string[] args) {
      Console.WriteLine("Hello World!");

      B2BReceivingProcessing B2BProc = new B2BReceivingProcessing(
          new CashCodeProtocol.B2B.CashCodeB2BCfg { DecicePort = "COM4" }, null);
      B2BProc.Connect();
      B2BProc.StartPollingLoop();
      B2BProc.OnPackedOrStacked += (D, d) => { Console.WriteLine($"TV:{D.TotalValue}"); Console.WriteLine($"TP:{D.Count}"); };
      while (true) {
        var K = Console.ReadKey().Key;
        if (K == ConsoleKey.Q) {
          B2BProc.Dispose();
          break;
        }
        else if (K == ConsoleKey.R) B2BProc.SendReturn();
        else if (K == ConsoleKey.P) B2BProc.SendStack();
        else if (K == ConsoleKey.B) B2BProc.EnableReceving();
        else if (K == ConsoleKey.D) B2BProc.DisableReveving();
        else if (K == ConsoleKey.Enter) B2BProc.SendReset();
      }
      //Console.ReadKey();

    }
  }
}
