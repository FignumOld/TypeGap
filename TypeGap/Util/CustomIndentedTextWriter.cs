using System.Diagnostics;
using System;
using System.IO;
using System.Text;
using System.Globalization;

namespace TypeGap.Util
{

    // from https://referencesource.microsoft.com/#System/compmod/system/codedom/compiler/IndentTextWriter.cs,bae755007e6f4473
    // needed to copy here because not available in dnc
    public class CustomIndentedTextWriter : TextWriter
    {
        private TextWriter writer;
        private int indentLevel;
        private bool tabsPending;
        private string tabString;

        public const string DefaultTabString = "    ";

        public CustomIndentedTextWriter(TextWriter writer) : this(writer, DefaultTabString)
        {
        }
        public CustomIndentedTextWriter(TextWriter writer, string tabString) : base(CultureInfo.InvariantCulture)
        {
            this.writer = writer;
            this.tabString = tabString;
            indentLevel = 0;
            tabsPending = false;
        }
        public override Encoding Encoding
        {
            get
            {
                return writer.Encoding;
            }
        }
        public override string NewLine
        {
            get
            {
                return writer.NewLine;
            }

            set
            {
                writer.NewLine = value;
            }
        }
        public int Indent
        {
            get
            {
                return indentLevel;
            }
            set
            {
                Debug.Assert(value >= 0, "Bogus Indent... probably caused by mismatched Indent++ and Indent--");
                if (value < 0)
                {
                    value = 0;
                }
                indentLevel = value;
            }
        }
        public TextWriter InnerWriter
        {
            get
            {
                return writer;
            }
        }
        internal string TabString
        {
            get { return tabString; }
        }
        public override void Flush()
        {
            writer.Flush();
        }
        protected virtual void OutputTabs()
        {
            if (tabsPending)
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    writer.Write(tabString);
                }
                tabsPending = false;
            }
        }
        public override void Write(string s)
        {
            OutputTabs();
            writer.Write(s);
        }
        public override void Write(bool value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(char value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(char[] buffer)
        {
            OutputTabs();
            writer.Write(buffer);
        }
        public override void Write(char[] buffer, int index, int count)
        {
            OutputTabs();
            writer.Write(buffer, index, count);
        }
        public override void Write(double value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(float value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(int value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(long value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(object value)
        {
            OutputTabs();
            writer.Write(value);
        }
        public override void Write(string format, object arg0)
        {
            OutputTabs();
            writer.Write(format, arg0);
        }
        public override void Write(string format, object arg0, object arg1)
        {
            OutputTabs();
            writer.Write(format, arg0, arg1);
        }
        public override void Write(string format, params object[] arg)
        {
            OutputTabs();
            writer.Write(format, arg);
        }
        public void WriteLineNoTabs(string s)
        {
            writer.WriteLine(s);
        }
        public override void WriteLine(string s)
        {
            OutputTabs();
            writer.WriteLine(s);
            tabsPending = true;
        }
        public override void WriteLine()
        {
            OutputTabs();
            writer.WriteLine();
            tabsPending = true;
        }
        public override void WriteLine(bool value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(char value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(char[] buffer)
        {
            OutputTabs();
            writer.WriteLine(buffer);
            tabsPending = true;
        }
        public override void WriteLine(char[] buffer, int index, int count)
        {
            OutputTabs();
            writer.WriteLine(buffer, index, count);
            tabsPending = true;
        }
        public override void WriteLine(double value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(float value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(int value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(long value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(object value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        public override void WriteLine(string format, object arg0)
        {
            OutputTabs();
            writer.WriteLine(format, arg0);
            tabsPending = true;
        }
        public override void WriteLine(string format, object arg0, object arg1)
        {
            OutputTabs();
            writer.WriteLine(format, arg0, arg1);
            tabsPending = true;
        }
        public override void WriteLine(string format, params object[] arg)
        {
            OutputTabs();
            writer.WriteLine(format, arg);
            tabsPending = true;
        }
        [CLSCompliant(false)]
        public override void WriteLine(UInt32 value)
        {
            OutputTabs();
            writer.WriteLine(value);
            tabsPending = true;
        }
        internal void InternalOutputTabs()
        {
            for (int i = 0; i < indentLevel; i++)
            {
                writer.Write(tabString);
            }
        }

        internal class Indentation
        {
            private CustomIndentedTextWriter writer;
            private int indent;
            private string s;

            internal Indentation(CustomIndentedTextWriter writer, int indent)
            {
                this.writer = writer;
                this.indent = indent;
                s = null;
            }

            internal string IndentationString
            {
                get
                {
                    if (s == null)
                    {
                        string tabString = writer.TabString;
                        StringBuilder sb = new StringBuilder(indent * tabString.Length);
                        for (int i = 0; i < indent; i++)
                        {
                            sb.Append(tabString);
                        }
                        s = sb.ToString();
                    }
                    return s;
                }
            }
        }
    }
}