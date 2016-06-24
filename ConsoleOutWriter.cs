using System;
using System.IO;
using System.Text;

namespace ConsoleManager
{
    internal class ConsoleOutWriter : TextWriter
    {
        public override Encoding Encoding => Console.OutputEncoding;


        public override void Write(string value) { FastConsole.Write(value); }

        public override void WriteLine(string value) { FastConsole.WriteLine(value); }
    }
}