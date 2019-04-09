using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace backup2 {
    public partial class Form1 : Form {
        TamPubWin1.LogFile log = new TamPubWin1.LogFile();
        CopyFolderDiff task = new CopyFolderDiff();

        public Form1() {
            InitializeComponent();
            log.richTextBox = richTextBox1;
            loadConfig();
            timer1.Enabled = true;
        }

        public void loadConfig() {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            textBox1.Text = TamPub1.ConfigFileXml.readString(filename, "FolderSource", "");
            textBox2.Text = TamPub1.ConfigFileXml.readString(filename, "FolderBackup", "");
            textBox3.Text = TamPub1.ConfigFileXml.readString(filename, "FolderDiff", "");
            textBox4.Text = TamPub1.ConfigFileXml.readString(filename, "repeatInterval", "");
            int c = TamPub1.ConfigFileXml.readInt32(filename, "NoprocessListCount", 0);
            for (int i = 0; i < c; i++) {
                listView1.Items.Add(TamPub1.ConfigFileXml.readString(filename, "NoprocessListItem" + i, ""));
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            FolderBrowserDialog f = new FolderBrowserDialog();
            if (f.ShowDialog() != DialogResult.OK) return;
            textBox2.Text = f.SelectedPath;

        }

        private void button1_Click(object sender, EventArgs e) {
            FolderBrowserDialog f = new FolderBrowserDialog();
            if (f.ShowDialog() != DialogResult.OK) return;
            textBox1.Text = f.SelectedPath;
        }

        private void button3_Click(object sender, EventArgs e) {
            FolderBrowserDialog f = new FolderBrowserDialog();
            if (f.ShowDialog() != DialogResult.OK) return;
            textBox3.Text = f.SelectedPath;

        }

        private void button4_Click(object sender, EventArgs e) {
            task = new CopyFolderDiff();
            task.folderSource = textBox1.Text;
            task.folderBackup = textBox2.Text;
            task.folderDiff = textBox3.Text;
            task.listView = listView1;
            try {
                task.repeatInterval = Convert.ToInt32(textBox4.Text);
            } catch {
                task.repeatInterval = 0;
            }

            Thread threadCopy = new Thread(new ThreadStart(task.copyFolderDiff));
            threadCopy.Name = "threadCopy";
            threadCopy.Priority = ThreadPriority.BelowNormal;
            threadCopy.Start();
        }

        public class CopyFolderDiff {
            public TamPub1.MemoryLog log = new TamPub1.MemoryLog();
            public string folderSource = "";
            public string folderBackup = "";
            public string folderDiff = "";
            private string folderName = "";
            private string folderDiffReal = "";
            public int backupCount = 0;
            public int totalCount = 0;
            public int repeatInterval = 0;
            public string statusString = "";
            public bool exit = false;
            public ListView listView = null;

            public CopyFolderDiff() {
                log.size = 3000;
            }

            private void init() {
                folderSource = TamPub1.StringFunc.deleteRearChar(folderSource.Trim(), '\\');
                folderBackup = TamPub1.StringFunc.deleteRearChar(folderBackup.Trim(), '\\');
                folderDiff = TamPub1.StringFunc.deleteRearChar(folderDiff.Trim(), '\\');
                int a = folderSource.LastIndexOf("\\");
                if (a < 0) {
                    folderName = "";
                    return;
                }
                folderName = folderSource.Substring(a + 1);
                if (folderDiff.Length > 0) {
                    DateTime currentTime = DateTime.Now;
                    folderDiffReal = folderDiff + "\\" + folderName + "\\" +
                        currentTime.ToString("yyyy_MM_dd") + "\\" +
                        currentTime.ToString("HH_mm_ss");
                }
            }

            public void copyFolderDiff() {
                while (true) {
                    init();
                    //log.writeWarning("start backup, source=" + folderSource + ",backup=" + folderBackup + ",diff=" + folderDiff);
                    statusString = "正在备份...";
                    backupCount = 0;
                    totalCount = 0;
                    try {
                        copyFolderDiffImp(folderSource);
                    } catch (Exception ee) {
                        log.writeWarning("备份过程中出现错误, error=" + ee.Message);
                    }
                    if (backupCount > 0) {
                        log.writeWarning("backup complete, totalCount=" + totalCount + ",backupCount=" + backupCount);
                    }
                    if (repeatInterval <= 0) break;
                    DateTime startTime = DateTime.Now;
                    while (true) {
                        statusString = "备份完成，离下一次备份还剩" + Math.Floor(repeatInterval - (DateTime.Now - startTime).TotalSeconds) + "秒";
                        if ((DateTime.Now - startTime).TotalSeconds >= repeatInterval) break;
                        if (exit) break;
                        Thread.Sleep(100);
                    }
                    if (exit) break;
                }
                statusString = "备份完成";
            }

            public void copyFolderDiffImp(string sfolder) {
                if (exit) return;
                DirectoryInfo dir = new DirectoryInfo(sfolder);
                //不是目录
                if (dir == null) return;
                FileSystemInfo[] files = dir.GetFileSystemInfos();
                for (int i = 0; i < files.Length; i++) {
                    FileInfo file = files[i] as FileInfo;
                    if (file != null) { //是文件
                        if (TamPub1.FileOperation.extractFileName(file.FullName).ToUpper().Equals("THUMBS.DB")) {
                            continue;
                        }
                        FileInfo df = new FileInfo(folderBackup + "\\" + folderName + file.FullName.Substring(folderSource.Length));
                        totalCount++;
                        bool needCopy = false;
                        if (df == null) {
                            needCopy = true;
                        } else if (file.LastWriteTime != df.LastWriteTime) {
                            needCopy = true;
                        }
                        if (needCopy) {
                            backupCount++;
                            System.IO.Directory.CreateDirectory(TamPub1.FileOperation.extractFilePath(df.FullName));
                            if (File.Exists(df.FullName) && folderDiff.Length > 0) {
                                //上一个文件拷贝到差异目录组
                                System.IO.Directory.CreateDirectory(TamPub1.FileOperation.extractFilePath(folderDiffReal + file.FullName.Substring(folderSource.Length)));
                                File.Copy(df.FullName, folderDiffReal + file.FullName.Substring(folderSource.Length), true);
                            }
                            File.Copy(file.FullName, df.FullName, true);
                            log.writeCommon("backup file: " + file.FullName);
                        }
                    } else {  //对于子目录
                        dir = files[i] as DirectoryInfo;
                        if (needProcess(dir.FullName)) {
                            copyFolderDiffImp(dir.FullName);
                        }
                    }
                }
            }
            private bool needProcess(string filePath) {
                filePath = filePath.ToUpper();
                filePath = TamPub1.StringFunc.deleteRearChar(filePath, '\\');
                for (var i = 0; i < listView.Items.Count; i++) {
                    if (listView.Items[i].Text.ToUpper().Equals(filePath)) {
                        return false;
                    }
                }
                return true;
            }
        }

        private void timer1_Tick(object sender, EventArgs e) {
            //process log
            var count = 0;
            log.datetimeType = 0;
            while (task.log.pop()) {
                log.writeLogString(task.log.current.logType, task.log.current.logString);
                count++;
                if (count > 100) break;
            }
            log.datetimeType = 10;

            //process status
            if (!toolStripStatusLabel1.Text.Equals(task.statusString)) {
                toolStripStatusLabel1.Text = task.statusString;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeString(filename, "FolderSource", textBox1.Text);
        }

        private void Form1_Load(object sender, EventArgs e) {
            TamPubWin1.Etc.loadDesktop(TamPub1.FileOperation.currentFilePath + "config.xml", this);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            TamPubWin1.Etc.saveDesktop(TamPub1.FileOperation.currentFilePath + "config.xml", this);
            task.exit = true;
        }

        private void textBox2_TextChanged(object sender, EventArgs e) {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeString(filename, "FolderBackup", textBox2.Text);
        }

        private void textBox3_TextChanged(object sender, EventArgs e) {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeString(filename, "FolderDiff", textBox3.Text);
        }

        private void textBox4_TextChanged(object sender, EventArgs e) {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeString(filename, "repeatInterval", textBox4.Text);
        }

        private void a增加ToolStripMenuItem_Click(object sender, EventArgs e) {
            FolderBrowserDialog f = new FolderBrowserDialog();
            if (f.ShowDialog() != DialogResult.OK) return;
            ListViewItem item = new ListViewItem();
            item.Text = f.SelectedPath;
            listView1.Items.Add(item);
            saveNoprocessList();
        }

        private void d删除ToolStripMenuItem_Click(object sender, EventArgs e) {
            int index = 0;
            while (true) {
                if (index >= listView1.Items.Count) break;
                if (listView1.Items[index].Selected) {
                    listView1.Items.RemoveAt(index);
                } else {
                    index++;
                }
            }
            saveNoprocessList();
        }

        private void saveNoprocessList() {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeInt32(filename, "NoprocessListCount", listView1.Items.Count);
            for (int i = 0; i < listView1.Items.Count; i++) {
                TamPub1.ConfigFileXml.writeString(filename, "NoprocessListItem" + i, listView1.Items[i].Text);
            }
        }
    }
}

