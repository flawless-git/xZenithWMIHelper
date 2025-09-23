using System;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.IO;

namespace xZenithWMIHelper
{
    public class xZenithWMIHelper
    {
        private static ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xZenithWMIHelper.log");

        public static void Main(string[] args)
        {
            // Log to file instead of console since we're running in background
            LogToFile("WMI Helper application started");
            
            // Create a stop file watcher to allow graceful shutdown
            string stopFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stop.txt");
            
            // Set up a file watcher to detect when to exit
            var fileWatcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory);
            fileWatcher.Created += (sender, e) => {
                if (e.Name.Equals("stop.txt", StringComparison.OrdinalIgnoreCase))
                {
                    _exitEvent.Set();
                }
            };
            fileWatcher.EnableRaisingEvents = true;
            
            // Start the WMI event listener
            WmiEventListener.StartWmiEventListener();
            
            LogToFile("Application running in background. Create a file named 'stop.txt' in the application directory to stop.");
            
            // Wait for exit signal
            _exitEvent.WaitOne();
            
            // Stop the WMI event listener before exiting
            WmiEventListener.StopWmiEventListener();
            
            // Clean up the stop file if it exists
            try {
                if (File.Exists(stopFilePath))
                {
                    File.Delete(stopFilePath);
                }
            } catch {}
            
            LogToFile("WMI Helper application stopped");
        }
        
        private static void LogToFile(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now}] {message}";
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    public class WmiEventListener
    {
        private static ManagementEventWatcher watcher;
        private static string eventLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wmi_events.log");

        public static void StartWmiEventListener()
        {
            try
            {
                // Pastikan file log kosong saat memulai
                File.WriteAllText(eventLogFilePath, "");
                
                var query = new WqlEventQuery("SELECT * FROM IP3_WMIEvent");
                watcher = new ManagementEventWatcher(new ManagementScope("root\\WMI"), query);
                
                watcher.EventArrived += (sender, e) => 
                {
                    try {
                        // When event is received, send as JSON to output file
                        var wmiEvent = e.NewEvent;
                        var eventDetail = (byte[])wmiEvent.Properties["EventDetail"].Value;
                        var dataAsIntArray = eventDetail.Select(b => (int)b).ToArray();
                        
                        var response = new { 
                            type = "WMI_EVENT", 
                            data = dataAsIntArray,
                            message = "WMI Event received",
                            details = $"Event received at {DateTime.Now}"
                        };
                        
                        SendResponseToTauri(response);
                        LogToFile($"Event received and logged: {dataAsIntArray.Length} bytes");
                    }
                    catch (Exception ex) {
                        LogToFile($"Error processing event: {ex.Message}");
                    }
                };
                
                watcher.Start();
                LogToFile("WMI event watcher started successfully");
                
                // Kirim event test untuk memastikan koneksi berfungsi
                var testEvent = new { 
                    type = "WMI_TEST", 
                    message = "WMI Event Listener Test",
                    details = "This is a test event to verify the connection"
                };
                SendResponseToTauri(testEvent);
            }
            catch (Exception ex)
            {
                // Failed to start watcher, log error
                LogToFile($"Failed to start WMI event watcher: {ex.Message}");
            }
        }

        public static void StopWmiEventListener()
        {
            try
            {
                if (watcher != null)
                {
                    watcher.Stop();
                    watcher.Dispose();
                    watcher = null;
                    LogToFile("WMI event watcher stopped successfully");
                    
                    // Kirim event penutup
                    var closeEvent = new { 
                        type = "WMI_CLOSE", 
                        message = "WMI Event Listener Closed",
                        details = "The event listener has been stopped"
                    };
                    SendResponseToTauri(closeEvent);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to stop WMI event watcher: {ex.Message}");
            }
        }

        private static void SendResponseToTauri(object data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                // Write to event log file
                File.AppendAllText(eventLogFilePath, json + Environment.NewLine);
                LogToFile($"Event logged to file: {eventLogFilePath}");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to serialize response: {ex.Message}");
            }
        }
        
        private static void LogToFile(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now}] {message}";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xZenithWMIHelper.log"), logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}