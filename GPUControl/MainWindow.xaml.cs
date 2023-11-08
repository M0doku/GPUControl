using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NvAPIWrapper.GPU;
using System.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using Forms = System.Windows.Forms;
using IWshRuntimeLibrary;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace GPUControl
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Process[] pname;
        private string programFullpath;
        private readonly Forms.NotifyIcon notifyIcon;       
        public MainWindow()
        {
            InitializeComponent();
            Uri iconUri = new Uri("Resources/icon.ico", UriKind.RelativeOrAbsolute);
            this.Icon = BitmapFrame.Create(iconUri);
            notifyIcon = new Forms.NotifyIcon();
            PhysicalGPU[] gpus = PhysicalGPU.GetPhysicalGPUs();
            foreach (PhysicalGPU gpu in gpus)
            {
                GPUName.Text = "Name : " + gpu.FullName;
            }
            if (Properties.Settings.Default["programName"].ToString() != null)
                programName.Text = Properties.Settings.Default["programName"].ToString();
            else
                programName.Text = Properties.Settings.Default.programName;
            if (Properties.Settings.Default["tempGPU"].ToString() != null)
                tempGPU.Text = Properties.Settings.Default["tempGPU"].ToString();
            else
                tempGPU.Text = Properties.Settings.Default.tempGPU;
            if (Properties.Settings.Default["programFullPath"].ToString() != null)
                folderName.Text = Properties.Settings.Default["programFullPath"].ToString();
            else 
                folderName.Text = Properties.Settings.Default.programFullPath;   
          
                startup_CB.IsChecked = Convert.ToBoolean(Properties.Settings.Default["isStartup"]);


            updateGPUTemp();
            icon();
            updateProgramStatus();
        }
        private void updateGPUTemp()
        {
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    PhysicalGPU[] GPU = PhysicalGPU.GetPhysicalGPUs();
                    foreach (PhysicalGPU gpu in GPU)
                    {
                        foreach (GPUThermalSensor sensor in gpu.ThermalInformation.ThermalSensors)
                        {
                            Dispatcher.Invoke(() => currentGPUTemp.Text = "Temp : " + sensor.CurrentTemperature.ToString());
                            if (Dispatcher.Invoke(() => tempGPU.Text.Length > 0))
                            {
                                if (sensor.CurrentTemperature > Dispatcher.Invoke(() => Convert.ToInt32(tempGPU.Text)))
                                {
                                    gpuTempCheck();
                                }
                            }
                        }
                    }
                }
            }).Start();


        }
        private void updateProgramStatus()
        {
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(5000);
                    Dispatcher.Invoke(() => pname = Process.GetProcessesByName(programName.Text));
                    if (pname.Length == 0)
                       Dispatcher.Invoke(() => progStatus.Text = "Status: Disabled");
                    else
                        Dispatcher.Invoke(() => progStatus.Text = "Status: Enabled");
                }
            }).Start();
            

        }
        private void gpuTempCheck()
        {
            Dispatcher.Invoke(() => pname = Process.GetProcessesByName(programName.Text));
            if (pname.Length == 0)
                progLaunch();
            else { }
        }
        private void getFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.FileName = "Program";
            ofd.DefaultExt = ".exe";
            ofd.Filter = "Exe Files|*.exe";
            Nullable<bool> result = ofd.ShowDialog();
            if (result == true)
            {
                folderName.Text = ofd.FileName;
                int index = ofd.SafeFileName.IndexOf(".");
                programName.Text = ofd.SafeFileName.Substring(0, index);
            }
        }
        private void progLaunch()
        {
            try
            {            
                Process pc = new Process();
                pc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pc.StartInfo.CreateNoWindow = true; 
                pc.StartInfo.FileName = programFullpath;
                pc.Start();
            }
            catch
            {
                System.Windows.MessageBox.Show("Program not detected");
            }   
        }

        private void folderName_TextChanged(object sender, TextChangedEventArgs e)
        {
            programFullpath = folderName.Text;
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default["isStartup"] = startup_CB.IsChecked;
            Properties.Settings.Default["programFullPath"] = programFullpath;
            Properties.Settings.Default["programName"] = programName.Text;            
            Properties.Settings.Default["tempGPU"] = tempGPU.Text;
            Properties.Settings.Default.Save();
            e.Cancel = true;
            this.Hide();
            
        }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Minimized) this.Hide() ;
                
                base.OnStateChanged(e);
            }
            catch { }
        }
        private void icon()
        {            
            notifyIcon.Icon = new System.Drawing.Icon("Resources/icon.ico");
            notifyIcon.Visible = true;
            notifyIcon.Text = "GPUControl";
            notifyIcon.DoubleClick += NotifyIcon_Clicked;
            notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Close App", System.Drawing.Image.FromFile("Resources/icon.ico"), OnStatusClicked);
            
        }
        private void OnStatusClicked(object sender, EventArgs e) 
        {
            System.Windows.Application.Current.Shutdown();
        }
        private void NotifyIcon_Clicked(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
       /* public static void createShortcut(string ShortcutPath, string TargetPath)
        {
            WshShell wshshell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)wshshell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = TargetPath;
            string loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(loc);
            shortcut.Save();
        }*/
       public void createTask()
        {
            using (TaskService taskService = new TaskService())
            {
                
                TaskDefinition td = taskService.NewTask();
                td.RegistrationInfo.Description = "Launches GPUControl";
                td.Triggers.Add(new LogonTrigger { Enabled = true });
                string exefile = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string workingdirectory = System.IO.Path.GetDirectoryName(exefile);
                td.Actions.Add(new ExecAction(exefile, null, workingdirectory));
                td.Principal.RunLevel = TaskRunLevel.Highest;
                
                taskService.RootFolder.RegisterTaskDefinition(@"GPUControl", td);
            }
        }
        private void deleteTask()
        {
            using (TaskService taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask("GPUControl");
            }
        }
        private void startup_CB_Checked(object sender, RoutedEventArgs e)
        {           
           createTask();
        }
        private void startup_CB_Unchecked(object sender, RoutedEventArgs e)
        {
            deleteTask();
        }
        private void clearSettings_Click(object sender, RoutedEventArgs e)
        {
            programName.Text = "Program Name";
            folderName.Text = "Folder Path";
            tempGPU.Text = "70";
            startup_CB.IsChecked = false;  
            Properties.Settings.Default.Reset(); 
        }

    }
}
