// 
// Copyright (c) 2004-2010 Jaroslaw Kowalski <jaak@jkowalski.net>
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NLog.Common;
using NLog.Config;

namespace NLog.LayoutRenderers
{
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Exception information provided through 
    /// a call to one of the Logger.*Exception() methods.
    /// </summary>
    [LayoutRenderer("exception")]
    public class ExceptionLayoutRenderer : LayoutRenderer
    {
        private string format;
        private ExceptionDataTarget[] exceptionDataTargets = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionLayoutRenderer" /> class.
        /// </summary>
        public ExceptionLayoutRenderer()
        {
            this.Format = "message";
            this.Separator = " ";
        }

        private delegate void ExceptionDataTarget(StringBuilder sb, Exception ex);

        /// <summary>
        /// Gets or sets the format of the output. Must be a comma-separated list of exception
        /// properties: Message, Type, ShortType, ToString, Method, StackTrace.
        /// This parameter value is case-insensitive.
        /// </summary>
        [DefaultParameter]
        public string Format
        {
            get
            {
                return this.format;
            }

            set
            {
                this.format = value;
                this.CompileFormat(value);
            }
        }

        /// <summary>
        /// Gets or sets the separator used to concatenate parts specified in the Format.
        /// </summary>
        public string Separator { get; set; }

        /// <summary>
        /// Returns the estimated number of characters that are needed to
        /// hold the rendered value for the specified logging event.
        /// </summary>
        /// <param name="logEvent">Logging event information.</param>
        /// <returns>The number of characters.</returns>
        /// <remarks>
        /// If the exact number is not known or
        /// expensive to calculate this function should return a rough estimate
        /// that's big enough in most cases, but not too big, in order to conserve memory.
        /// </remarks>
        protected override int GetEstimatedBufferSize(LogEventInfo logEvent)
        {
            return 32;
        }

        /// <summary>
        /// Renders the specified exception information and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder"/> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            if (logEvent.Exception != null)
            {
                StringBuilder sb2 = new StringBuilder(128);

                for (int i = 0; i < this.exceptionDataTargets.Length; ++i)
                {
                    if (i != 0)
                    {
                        sb2.Append(this.Separator);
                    }

                    this.exceptionDataTargets[i](sb2, logEvent.Exception);
                }

                builder.Append(sb2.ToString());
            }
        }

        /// <summary>
        /// Determines whether the layout renderer is volatile.
        /// </summary>
        /// <returns>
        /// A boolean indicating whether the layout renderer is volatile.
        /// </returns>
        /// <remarks>
        /// Volatile layout renderers are dependent on information not contained
        /// in <see cref="LogEventInfo"/> (such as thread-specific data, MDC data, NDC data).
        /// </remarks>
        public override bool IsVolatile()
        {
            return false;
        }

        private void AppendMessage(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.Message);
        }

        private void AppendMethod(StringBuilder sb, Exception ex)
        {
#if SILVERLIGHT || NET_CF
            sb.Append(ParseMethodNameFromStackTrace(ex.StackTrace));
#else
            sb.Append(ex.TargetSite.ToString());
#endif
        }

        private void AppendStackTrace(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.StackTrace);
        }

        private void AppendToString(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.ToString());
        }

        private void AppendType(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.GetType().FullName);
        }

        private void AppendShortType(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.GetType().Name);
        }

        private void CompileFormat(string format)
        {
            string[] parts = format.Replace(" ", string.Empty).Split(',');
            List<ExceptionDataTarget> dataTargets = new List<ExceptionDataTarget>();

            foreach (string s in parts)
            {
                switch (s.ToLower(CultureInfo.InvariantCulture))
                {
                    case "message":
                        dataTargets.Add(this.AppendMessage);
                        break;

                    case "type":
                        dataTargets.Add(this.AppendType);
                        break;

                    case "shorttype":
                        dataTargets.Add(this.AppendShortType);
                        break;

                    case "tostring":
                        dataTargets.Add(this.AppendToString);
                        break;

                    case "method":
                        dataTargets.Add(this.AppendMethod);
                        break;

                    case "stacktrace":
                        dataTargets.Add(this.AppendStackTrace);
                        break;

                    default:
                        InternalLogger.Warn("Unknown exception data target: {0}", s);
                        break;
                }
            }

            this.exceptionDataTargets = dataTargets.ToArray();
        }

#if SILVERLIGHT || NET_CF
        private string ParseMethodNameFromStackTrace(string stackTrace)
        {
            // get the first line of the stack trace
            string stackFrameLine;

            int p = stackTrace.IndexOfAny(new[] { '\r', '\n' });
            if (p >= 0)
            {
                stackFrameLine = stackTrace.Substring(0, p);
            }
            else
            {
                stackFrameLine = stackTrace;
            }

            // stack trace is composed of lines which look like this
            //
            // at NLog.UnitTests.LayoutRenderers.ExceptionTests.GenericClass`3.Method2[T1,T2,T3](T1 aaa, T2 b, T3 o, Int32 i, DateTime now, Nullable`1 gfff, List`1[] something)
            //
            // "at " prefix can be localized so we cannot hard-code it but it's followed by a space, class name (which does not have a space in it) and opening paranthesis
            int lastSpace = -1;
            int startPos = 0;
            int endPos = stackFrameLine.Length;

            for (int i = 0; i < stackFrameLine.Length; ++i)
            {
                switch (stackFrameLine[i])
                {
                    case ' ':
                        lastSpace = i;
                        break;

                    case '(':
                        startPos = lastSpace + 1;
                        break;

                    case ')':
                        endPos = i + 1;

                        // end the loop
                        i = stackFrameLine.Length;
                        break;
                }
            }

            return stackTrace.Substring(startPos, endPos - startPos);
        }
#endif
    }
}
