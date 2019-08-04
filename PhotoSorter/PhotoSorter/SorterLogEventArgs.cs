using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorter
{
    /// <summary>
    /// Event args for a Sorter log message
    /// </summary>
    public class SorterLogEventArgs : EventArgs
    {
        /// <summary>
        /// Log message
        /// </summary>
        public string LogMessage { get; set; }

        /// <summary>
        /// Event args for a Sorter log message
        /// </summary>
        /// <param name="logMessage">Log message</param>
        public SorterLogEventArgs(string logMessage)
        {
            LogMessage = logMessage;
        }
    }
}
