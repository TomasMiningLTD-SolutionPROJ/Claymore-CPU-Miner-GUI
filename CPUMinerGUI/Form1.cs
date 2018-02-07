using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
/*
no fool-proof verification of entered data
no precise calculation of total hashrate
when programm starts it tries to delete prev log files, if it fails - it fails =)
*/
namespace CPUMinerGUI {
    public partial class Form1:Form {
        private List<PoolItem> poolList;
        private PoolItem curItem;
        private List<Miner> miners;
        private Miner curMiner;
        private string fullHashrate;
        
        private bool waitingForLog = false;
        private Miner waitingMiner;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("User32")]
        private static extern int ShowWindow(int nCmdShow, int hwnd);

        public Form1() {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, EventArgs e) {
            if (addressText.Text != String.Empty &&
                userText.Text != String.Empty &&
                passwordText.Text != String.Empty) {

                poolList.Add(new PoolItem(addressText.Text, userText.Text, passwordText.Text, notesText.Text));
                WriteList();
                ReadList();
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            poolList = new List<PoolItem>();
            miners = new List<Miner>();
            ReadList();

            DirectoryInfo dir = new DirectoryInfo(Application.StartupPath);

            foreach (FileInfo file in dir.EnumerateFiles("*_log.txt")) {
                file.Delete();
            }

            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = Application.StartupPath,
                EnableRaisingEvents = true,
                Filter = "*_log.txt"
            };
            watcher.Created += new FileSystemEventHandler(NewLogFound);

            hashChart.Series[0].YAxisType = AxisType.Primary;
            hashChart.Series[0].YValueType = ChartValueType.Single;
            hashChart.Series[0].IsXValueIndexed = false;

            hashChart.ResetAutoValues();
            hashChart.ChartAreas[0].AxisY.Maximum = 1;
            hashChart.ChartAreas[0].AxisY.Minimum = 0;
            hashChart.ChartAreas[0].AxisX.Enabled = AxisEnabled.False;
            hashChart.ChartAreas[0].AxisY.IntervalAutoMode = IntervalAutoMode.FixedCount;
        }

        void NewLogFound(object s, FileSystemEventArgs e) {
            if (waitingForLog == false) return;

            if (InvokeRequired) {
                object[] args = { s, e };
                Invoke(new FileSystemEventHandler(NewLogFound), args);
            } else {
                waitingMiner.logFile = e.Name;
                miners.Add(waitingMiner);

                RefreshInstanceList();
                instancesList.SelectedIndex = instancesList.Items.Count - 1;

                waitingForLog = false;
            }

        }

        private void ReadList() {
            poolList.Clear();
            poolsList.Items.Clear();
            PoolItem.resetId();

            addressText.Text = String.Empty;
            userText.Text = String.Empty;
            passwordText.Text = String.Empty;
            notesText.Text = String.Empty;
            removeButton.Enabled = false;
            saveButton.Enabled = false;
            startButton.Enabled = false;

            string[] items;
            try {
                items = File.ReadAllLines("pool_list.txt");
            } catch (IOException) {
                items = new string[0];
            }
            

            if (items.Length == 0) return;

            foreach (string item in items) {
                string[] itemParams = item.Split('|');
                poolList.Add(new PoolItem(itemParams[0], itemParams[1], itemParams[2], itemParams[3]));
            }

            foreach (PoolItem item in poolList) {
                poolsList.Items.Add("[" + item.id + "] " + item.address + " -=::=- " + item.description);
            }
        }

        private void WriteList() {
            string[] items = new string[poolList.Count];
            int i = 0;

            foreach (PoolItem item in poolList) {
                items[i++] = item.ToString();
            }

            File.WriteAllLines("pool_list.txt", items);
        }

        private void PoolsList_SelectedIndexChanged(object sender, EventArgs e) {
#pragma warning disable CS0472 // The result of the expression is always the same since a value of this type is never equal to 'null'
            if (poolsList.SelectedIndex == -1 || poolsList.SelectedIndex == null) return;
#pragma warning restore CS0472 // The result of the expression is always the same since a value of this type is never equal to 'null'

            string selItem = poolsList.SelectedItem as string;

            int id = int.Parse(selItem.Split(']')[0].Substring(1));
            curItem = poolList.Find(x => x.id == id);

            addressText.Text = curItem.address;
            userText.Text = curItem.user;
            passwordText.Text = curItem.password;
            notesText.Text = curItem.description;

            removeButton.Enabled = true;
            saveButton.Enabled = true;
            startButton.Enabled = true;
        }

        private void RemoveButton_Click(object sender, EventArgs e) {
            poolList.Remove(curItem);
            WriteList();
            ReadList();
        }

        private void SaveButton_Click(object sender, EventArgs e) {
            if (addressText.Text != String.Empty &&
                userText.Text != String.Empty &&
                passwordText.Text != String.Empty) {

                curItem.address = addressText.Text;
                curItem.user = userText.Text;
                curItem.password = passwordText.Text;
                curItem.description = notesText.Text;

                WriteList();
                ReadList();
            }

        }

        private void StartButton_Click(object sender, EventArgs e) {
            if (waitingForLog) return;

            string command = "-o " + curItem.address + " -u " + curItem.user + " -p " + curItem.password +
                             (lowCpuCheck.Checked ? " -lowcpu " + lowCPUText.Text : " -t " + threadsText.Text);
            
            Process p = new Process();
            p.StartInfo.FileName = win32Check.Checked ? "NsCpuCNMiner32.exe" : "NsCpuCNMiner64.exe";
            p.StartInfo.Arguments = command;
            p.StartInfo.UseShellExecute = false;

            p.Start();

            waitingForLog = true;
           
            waitingMiner = new Miner(p.Id, threadsText.Text, lowCpuCheck.Checked, curItem, p, "log");
        }

        private void RefreshInstanceList() {
            instancesList.Items.Clear();
            
            foreach (Miner miner in miners) {
                
                instancesList.Items.Add("[" + miner.pid + "]" +
                    " {" + miner.hashrate + "} " +
                    "[" + (miner.lowCpu ? "low" : miner.threads) + "] " +
                    (miner.pool.description != String.Empty ? miner.pool.description : miner.pool.address));
            }

            showButton.Enabled = false;
            hideButton.Enabled = false;
            stopButton.Enabled = false;

            priorityDropdown.Enabled = false;
            priorityDropdown.SelectedIndex = -1;

            hashrateTimer.Enabled = miners.Count > 0;
        }

        private void InstancesList_SelectedIndexChanged(object sender, EventArgs e) {
            if (instancesList.SelectedIndex == -1) return;

            showButton.Enabled = true;
            hideButton.Enabled = true;
            stopButton.Enabled = true;

            string selItem = instancesList.SelectedItem as string;
            int pid = int.Parse(selItem.Split(']')[0].Substring(1));
            curMiner = miners.Find(x => x.pid == pid);

            priorityDropdown.Enabled = true;
            priorityDropdown.SelectedIndex = curMiner.priority;
        }

        private void ShowButton_Click(object sender, EventArgs e) {
            int hWnd = curMiner.proc.MainWindowHandle.ToInt32();
            ShowWindow(SW_SHOW, hWnd);
        }

        private void HideButton_Click(object sender, EventArgs e) {
            int hWnd = curMiner.proc.MainWindowHandle.ToInt32();
            ShowWindow(SW_HIDE, hWnd);
        }

        private void StopButton_Click(object sender, EventArgs e) {
            curMiner.proc.Kill();

            miners.Remove(curMiner);
            RefreshInstanceList();
        }

        private void HashrateTimer_Tick(object sender, EventArgs e) {
            Random rnd = new Random();
            fullHashrate = "0 h/s";

            foreach (Miner miner in miners) {
                string hashrate = ParseLogTail(miner.logFile);

                miner.hashrate = (hashrate != String.Empty ? hashrate : miner.hashrate);
                SummHashrate(miner.hashrate);

                int idx = instancesList.FindString("[" + miner.pid + "]");
                instancesList.Items[idx] = "[" + miner.pid + "]" +
                    " {" + miner.hashrate + "} " +
                    "[" + (miner.lowCpu ? "low" : miner.threads) + "] " +
                    (miner.pool.description != String.Empty ? miner.pool.description : miner.pool.address);
            }

            if (fullHashrate == fullHashrateLabel.Text) return;

            fullHashrateLabel.Text = fullHashrate;

            float hr = float.Parse(fullHashrate.Split(' ')[0]);

            if (hr > hashChart.ChartAreas[0].AxisY.Maximum) hashChart.ChartAreas[0].AxisY.Maximum = hr;

            hashChart.Series[0].Points.AddY(hr);
            if (hashChart.Series[0].Points.Count > 100) hashChart.Series[0].Points.RemoveAt(0);
        }

        private void SummHashrate(string hashrate) {
            float rate = float.Parse(hashrate.Split(' ')[0]);
            fullHashrate = float.Parse(fullHashrate.Split(' ')[0]) + rate + " " + hashrate.Split(' ')[1];
        }

        private string ParseLogTail(string file) {
            string[] lines;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs)) {
                string wholeLog = sr.ReadToEnd();
                char[] split = new char[2];
                split[0] = '\r';
                split[1] = '\n';
                lines = wholeLog.Split(split);
            }

            string hashrate = "";

            for (int i = lines.Length - 1; i > 0; i--) {
                if (lines[i].Contains("Speed: ")) {
                    string[] line = lines[i].Split('\t');

                    hashrate = line[2].Substring(7, line[2].IndexOf(',') - 7);

                    break;
                }
            }

            return hashrate;
        }

        private void PriorityDropdown_SelectedIndexChanged(object sender, EventArgs e) {
            switch (priorityDropdown.SelectedIndex) {
                case 0:
                    curMiner.priority = 0;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.RealTime;
                    break;
                case 1:
                    curMiner.priority = 1;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.High;
                    break;
                case 2:
                    curMiner.priority = 2;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case 3:
                    curMiner.priority = 3;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.Normal;
                    break;
                case 4:
                    curMiner.priority = 4;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
                case 5:
                    curMiner.priority = 5;
                    curMiner.proc.PriorityClass = ProcessPriorityClass.Idle;
                    break;
                default:
                    break;
            }
        }

        private void MoveUpButton_Click(object sender, EventArgs e) {
            if (poolsList.SelectedIndex == -1 || poolsList.SelectedIndex == 0) return;

            string tmp = poolsList.Items[poolsList.SelectedIndex - 1] as string;
            poolsList.Items[poolsList.SelectedIndex - 1] = poolsList.Items[poolsList.SelectedIndex];
            poolsList.Items[poolsList.SelectedIndex] = tmp;

            PoolItem tmpItem = poolList[poolsList.SelectedIndex - 1] as PoolItem;
            poolList[poolsList.SelectedIndex - 1] = poolList[poolsList.SelectedIndex];
            poolList[poolsList.SelectedIndex] = tmpItem;
            WriteList();

            poolsList.SelectedIndex--;
        }

        private void MoveDownButton_Click(object sender, EventArgs e) {
            if (poolsList.SelectedIndex == -1 || poolsList.SelectedIndex == poolsList.Items.Count - 1) return;

            string tmp = poolsList.Items[poolsList.SelectedIndex + 1] as string;
            poolsList.Items[poolsList.SelectedIndex + 1] = poolsList.Items[poolsList.SelectedIndex];
            poolsList.Items[poolsList.SelectedIndex] = tmp;

            PoolItem tmpItem = poolList[poolsList.SelectedIndex + 1] as PoolItem;
            poolList[poolsList.SelectedIndex + 1] = poolList[poolsList.SelectedIndex];
            poolList[poolsList.SelectedIndex] = tmpItem;
            WriteList();

            poolsList.SelectedIndex++;
        }
    }
}
