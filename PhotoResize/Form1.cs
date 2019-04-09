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

namespace PhotoResize {
    public partial class Form1 : Form {
        TamPubWin1.LogFile log = new TamPubWin1.LogFile();
        ImageResize task = new ImageResize();

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
            textBox4.Text = TamPub1.ConfigFileXml.readString(filename, "repeatInterval", "");
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
        }

        private void button4_Click(object sender, EventArgs e) {
            task = new ImageResize();
            task.folderSource = textBox1.Text;
            task.folderBackup = textBox2.Text;
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

        public class ImageResize {
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

            public ImageResize() {
                log.size = 3000;
            }

            private void init() {
                folderSource = TamPub1.StringFunc.deleteRearChar(folderSource.Trim(), '\\');
                folderBackup = TamPub1.StringFunc.deleteRearChar(folderBackup.Trim(), '\\');
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
                    statusString = "正在压缩图片...";
                    backupCount = 0;
                    totalCount = 0;
                    try {
                        copyFolderDiffImp(folderSource);
                    } catch (Exception ee) {
                        log.writeWarning("压缩过程中出现错误, error=" + ee.Message + ",trace=" + ee.StackTrace);
                    }
                    if (backupCount > 0) {
                        log.writeWarning("backup complete, totalCount=" + totalCount + ",backupCount=" + backupCount);
                    }
                    if (repeatInterval <= 0) break;
                    DateTime startTime = DateTime.Now;
                    while (true) {
                        statusString = "压缩完成，离下一次压缩还剩" + Math.Floor(repeatInterval - (DateTime.Now - startTime).TotalSeconds) + "秒";
                        if ((DateTime.Now - startTime).TotalSeconds >= repeatInterval) break;
                        if (exit) break;
                        Thread.Sleep(100);
                    }
                    if (exit) break;
                }
                statusString = "图片压缩完成";
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
                        string ext = System.IO.Path.GetExtension(file.FullName).ToLower();
                        if (!ext.Equals(".jpg") &&
                            !ext.Equals(".jpeg")) continue;
                        string destFilename = folderBackup + "\\" + folderName + file.FullName.Substring(folderSource.Length);
                        totalCount++;
                        bool needCopy = false;
                        if (!File.Exists(destFilename)) {
                            needCopy = true;
                        }
                        if (needCopy) {
                            backupCount++;
                            System.IO.Directory.CreateDirectory(TamPub1.FileOperation.extractFilePath(destFilename));
                            //拷贝源文件
                            File.Copy(file.FullName, destFilename, true);
                            //压缩文件
                            string hdr = TamPub1.StringFunc.copy(TamPub1.FileOperation.extractFileName(file.FullName), 0, 2).ToLower();
                            var xSize = 800;
                            if (hdr.Equals("cb")) { //背景
                                xSize = 800;
                            } else if (hdr.Equals("cf")) { //头像
                                xSize = 400;
                            } else if (hdr.Equals("id")) { //身份证
                                xSize = 1500;
                            } else if (hdr.Equals("cl")) { //行驶证
                                xSize = 1500;
                            } else if (hdr.Equals("dl")) { //驾驶证
                                xSize = 1500;
                            } else if (hdr.Equals("bk")) { //副刹车
                                xSize = 1000;
                            } else if (hdr.Equals("dr")) { //j.行车记录仪
                                xSize = 1000;
                            }

                            //比例缩放
                            Image initimage;
                            try {
                                initimage = Image.FromFile(file.FullName);
                            } catch {
                                FileInfo df = new FileInfo(file.FullName);
                                if (df.Length < 50000) {
                                    log.writeWarning("由于图片上传未完成(" + (df.Length / 1000) + "k)或者文件格式不对，无法处理: " + file.FullName);
                                } else {
                                    log.writeWarning("由于图片过大(" + (df.Length / 1000) + "k)或者文件格式不对，无法处理: " + file.FullName);
                                }
                                continue;
                            }
                            int newWidth = initimage.Width;
                            int newHeight = initimage.Height;
                            if (newWidth > newHeight) {
                                if (newWidth > xSize) {
                                    newHeight = xSize * newHeight / newWidth;
                                    newWidth = xSize;
                                }
                            } else {
                                if (newHeight > xSize) {
                                    newWidth = xSize * newWidth / newHeight;
                                    newHeight = xSize;
                                }
                            }
                            //新建一个bmp图片
                            Image newimage = new Bitmap((int)newWidth, (int)newHeight);
                            //新建一个画板
                            Graphics newg = Graphics.FromImage(newimage);
                            //设置质量
                            newg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            newg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            //置背景色
                            newg.Clear(Color.White);
                            //画图
                            newg.DrawImage(initimage, new Rectangle(0, 0, newimage.Width, newimage.Height), new Rectangle(0, 0, initimage.Width, initimage.Height), GraphicsUnit.Pixel);
                            //保存缩略图
                            initimage.Dispose();
                            File.Delete(file.FullName);
                            newimage.Save(file.FullName, System.Drawing.Imaging.ImageFormat.Jpeg);
                            //释放资源
                            newg.Dispose();
                            newimage.Dispose();
                            GC.Collect();
                            log.writeCommon("拷贝并压缩文件: " + file.FullName);
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
        }

        private void textBox4_TextChanged(object sender, EventArgs e) {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
            TamPub1.ConfigFileXml.writeString(filename, "repeatInterval", textBox4.Text);
        }

        private void saveNoprocessList() {
            string filename = TamPub1.FileOperation.currentFilePath + "config.xml";
        }
    }
}

