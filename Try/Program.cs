namespace Try {
  using System;

  using CashCode.B2B.Receiving;

  class Program {
    static void Main(string[] args) {
      Console.WriteLine("Hello World!");

      B2BReceivingProcessing B2BProc = new B2BReceivingProcessing(
          new CashCodeProtocol.CashCodeB2BCfg { DecicePort = "COM4" }, null, null);
      B2BProc.Connect();
      B2BProc.StartPollingLoop();

      Console.ReadKey();
      B2BProc.Dispose();
    }
  }
}
