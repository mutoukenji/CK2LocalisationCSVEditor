using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace CK2CSVEDITOR
{
    public partial class mainForm : Form
    {
        private string openedFilePath = "";
        //TODO: codes和lines改用HashMap一个变量来管理
        // CODES
        List<string> codes = new List<string>();
        // 原始数据
        List<string[]> lines = new List<string[]>();
        // 顶部头（不要也无所谓）
        string header = "";
        // CODE对应列
        int codeColumn = 0;
        // 英语对应列
        int englishColumn = 1;

        public mainForm()
        {
            InitializeComponent();
            textContainer.AllowDrop = true;
            textContainer.DragEnter += new DragEventHandler(mainForm_DragEnter);
            textContainer.DragDrop += new DragEventHandler(mainForm_DragDrop);
            textContainer.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool showDialog = true;
            if (!openedFilePath.Equals(""))
            {
                showDialog = false;
                if (DialogResult.Yes == MessageBox.Show("如继续操作，则未保存的修改将会丢失，是否继续操作？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                {
                    showDialog = true;
                }
            }
            if (showDialog)
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Filter = "CK2 Localisation CSV|*.csv";
                openDialog.Multiselect = false;
                DialogResult openResult = openDialog.ShowDialog();
                if (openResult == DialogResult.OK)
                {
                    textContainer.Clear();
                    header = "";
                    codeColumn = 0;
                    englishColumn = 1;
                    codes.Clear();
                    lines.Clear();
                    loadFile(openDialog.FileName);
                    listData();
                }
            }
            updateSaveBtnState();
        }

        private void listData()
        {
            StringBuilder toDisplay = new StringBuilder();
            foreach (string[] line in lines)
            {
                toDisplay.Append(line[codeColumn]);
                toDisplay.Append(';');
                toDisplay.Append(line[englishColumn]);
                toDisplay.AppendLine();
            }
            textContainer.Text = toDisplay.ToString();
        }

        private void loadFile(string filePath)
        {
            FileStream fis = new FileStream(filePath, FileMode.Open);
            fis.Seek(0, SeekOrigin.Begin);
            byte[] buffer = new byte[10240];
            int lastCnt = 0;
            int readCnt = 0;

            while ((readCnt = fis.Read(buffer, lastCnt, buffer.Length - lastCnt)) > 0)
            {
                int newLastCnt = readCnt + lastCnt;
                int lineStart = 0;
                for (int i = 0; i < readCnt + lastCnt; i++)
                {
                    if (buffer[i] == '\n')
                    {
                        // 一行结束
                        byte[] lineBuffer = new byte[i - lineStart + 1];
                        for (int j = 0; j < lineBuffer.Length; j++)
                        {
                            lineBuffer[j] = buffer[j + lineStart];
                        }
                        StringBuilder str = new StringBuilder();
                        for (int j = 0; j < lineBuffer.Length; j++)
                        {
                            // 控制符
                            if (lineBuffer[j] == 0xA7)
                            {
                                switch (lineBuffer[j + 1])
                                {
                                    case (byte)'W':
                                        str.Append("<color white>");
                                        break;
                                    case (byte)'Y':
                                        str.Append("<color yellow>");
                                        break;
                                    case (byte)'G':
                                        str.Append("<color green>");
                                        break;
                                    case (byte)'R':
                                        str.Append("<color red>");
                                        break;
                                    case (byte)'B':
                                        str.Append("<color blue>");
                                        break;
                                    case (byte)'!':
                                        str.Append("</color>");
                                        break;
                                    default:
                                        str.Append("<color ");
                                        str.Append((char)lineBuffer[j + 1]);
                                        str.Append(">");
                                        break;
                                }
                                j += 1;
                            }
                            // 金钱符号
                            else if(lineBuffer[j] == 0xA4)
                            {
                                str.Append("<money/>");
                            }
                            // 双字节
                            else if (lineBuffer[j] < 0x00 || lineBuffer[j] > 0x7F)
                            {
                                string vchar = Encoding.Default.GetString(lineBuffer, j, 2);
                                str.Append(vchar);
                                j += 1;
                            }
                            // 单字节
                            else
                            {
                                str.Append((char)lineBuffer[j]);
                            }
                        }
                        string line = str.ToString();
                        if (header.Equals(""))
                        {
                            header = line;
                            string[] columns = line.Split(';');
                            for (int j = 0; j < columns.Length; j++)
                            {
                                if (columns[j].Equals("#CODE"))
                                {
                                    codeColumn = j;
                                }
                                else if (columns[j].Equals("ENGLISH"))
                                {
                                    englishColumn = j;
                                }
                            }
                        }
                        else
                        {
                            string[] columns = line.Split(';');
                            if (columns.Length <= englishColumn)
                            {
                                Console.Out.WriteLine("Format Error:" + line);
                            }
                            else
                            {
                                codes.Add(columns[codeColumn]);
                                lines.Add(columns);
                            }
                        }
                        newLastCnt -= lineBuffer.Length;
                        lineStart = i + 1;
                    }
                }
                lastCnt = newLastCnt;
                byte[] tmpBuffer = new byte[lastCnt];
                Array.Copy(buffer, lineStart, tmpBuffer, 0, lastCnt);
                Array.Clear(buffer, 0, buffer.Length);
                Array.Copy(tmpBuffer, buffer, tmpBuffer.Length);
            }
            openedFilePath = filePath;
            fis.Close();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 创建备份
            int extIndex = openedFilePath.LastIndexOf('.');
            string backPath = openedFilePath.Substring(0, extIndex) + ".bak";
            File.Copy(openedFilePath, backPath, true);
            string text = textContainer.Text;
            string[] newLines = text.Split('\n');
            // 重建文件
            FileStream fos = new FileStream(openedFilePath, FileMode.Create);
            fos.Seek(0, SeekOrigin.Begin);
            byte[] bHeader = Encoding.Default.GetBytes(header);
            int columnCnt = header.Split(';').Length;
            // 写入头（被注释行，可以不要）
            fos.Write(bHeader, 0, bHeader.Length);
            fos.WriteByte((byte)'\n');
            foreach (string newLine in newLines)
            {
                int codeEndIdx = newLine.IndexOf(';');
                if(codeEndIdx < 0)
                {
                    continue;
                }
                string code = newLine.Substring(0, codeEndIdx);
                string english = newLine.Substring(codeEndIdx + 1);
                byte[] bCode = Encoding.Default.GetBytes(code);
                byte[] bEnglish = new byte[english.Length * 2];
                int bEngLength = 0;
                string searchText = english;
                int searchResult = -1;
                while((searchResult = searchColorKeyWord(searchText,0)) >= 0)
                {
                    string before = searchText.Substring(0, searchResult);
                    string after = searchText.Substring(searchResult);
                    byte[] bBefore = Encoding.Default.GetBytes(before);
                    Array.Copy(bBefore, 0, bEnglish, bEngLength, bBefore.Length);
                    bEngLength += bBefore.Length;
                    // 金钱标记
                    if(after.StartsWith("<money/>",StringComparison.OrdinalIgnoreCase))
                    {
                        bEnglish[bEngLength++] = 0xA4;
                        // 去掉<money/>，共8个字符
                        searchText = after.Substring(8);
                    }
                    // 颜色标签关闭
                    else if (after.StartsWith("</color>",StringComparison.OrdinalIgnoreCase))
                    {
                        bEnglish[bEngLength++] = 0xA7;
                        bEnglish[bEngLength++] = (byte)'!';
                        // 去掉</color>，共8个字符
                        searchText = after.Substring(8);
                    }
                    // 颜色标签开始
                    else
                    {
                        int colorTagEnd = after.IndexOf('>');
                        string tag = after.Substring(0, colorTagEnd + 1);
                        string color = tag.Substring(7, tag.Length - 8);
                        char c = '\0';
                        switch(color)
                        {
                            case "yellow":
                            case "YELLOW":
                                c = 'Y';
                                break;
                            case "white":
                            case "WHITE":
                                c = 'W';
                                break;
                            case "green":
                            case "GREEN":
                                c = 'G';
                                break;
                            case "red":
                            case "RED":
                                c = 'R';
                                break;
                            case "blue":
                            case "BLUE":
                                c = 'B';
                                break;
                            default:
                                c = color.ToUpperInvariant()[0];
                                break;
                        }
                        bEnglish[bEngLength++] = 0xA7;
                        bEnglish[bEngLength++] = (byte)c;
                        // 去掉<color xxx>
                        searchText = after.Substring(colorTagEnd + 1);
                    }
                }
                byte[] bText = Encoding.Default.GetBytes(searchText);
                Array.Copy(bText, 0, bEnglish, bEngLength, bText.Length);
                bEngLength += bText.Length;

                for(int i = 0; i < columnCnt; i++)
                {
                    if(i == codeColumn)
                    {
                        fos.Write(bCode, 0, bCode.Length);
                    }
                    else if(i == englishColumn)
                    {
                        fos.Write(bEnglish, 0, bEngLength);
                    }
                    if(i < columnCnt-1)
                    {
                        fos.WriteByte((byte)';');
                    }
                    else
                    {
                        fos.WriteByte((byte)'x');
                    }
                }
                fos.WriteByte((byte)'\n');
            }
            fos.Flush();
            fos.Close();
        }

        private int searchColorKeyWord(string text,int start)
        {
            int idxColorStart = text.IndexOf("<color", start, StringComparison.OrdinalIgnoreCase);
            int idxColorEnd = text.IndexOf("</color>", start, StringComparison.OrdinalIgnoreCase);
            int idxMoney = text.IndexOf("<money/>", start, StringComparison.OrdinalIgnoreCase);
            if(idxColorStart >= 0 && idxColorEnd >= 0 && idxMoney >= 0)
            {
                return Math.Min(Math.Min(idxColorStart, idxColorEnd), idxMoney);
            }
            else if(idxColorStart >= 0 && idxMoney >= 0)
            {
                return Math.Min(idxColorStart, idxMoney);
            }
            else if(idxColorEnd >= 0 && idxMoney >= 0)
            {
                return Math.Min(idxColorEnd, idxMoney);
            }
            else if(idxColorStart >= 0 && idxColorEnd >= 0)
            {
                return Math.Min(idxColorStart, idxColorEnd);
            }
            else if(idxColorStart >= 0)
            {
                return idxColorStart;
            }
            else if(idxColorEnd >= 0)
            {
                return idxColorEnd;
            }
            else if(idxMoney >= 0)
            {
                return idxMoney;
            }
            else
            {
                return -1;
            }
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
            updateSaveBtnState();
        }

        private void updateSaveBtnState()
        {
            if(openedFilePath.Equals(""))
            {
                saveToolStripMenuItem.Enabled = false;
            }
            else
            {
                saveToolStripMenuItem.Enabled = true;
            }
        }

        private void mainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if(!files[0].EndsWith(".csv"))
            {
                MessageBox.Show("不支持的文件类型", "错误", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            else
            {
                bool open = true;
                if (!openedFilePath.Equals(""))
                {
                    open = false;
                    if (DialogResult.Yes == MessageBox.Show("如继续操作，则未保存的修改将会丢失，是否继续操作？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                    {
                        open = true;
                    }
                }
                if(open)
                {
                    header = "";
                    codeColumn = 0;
                    englishColumn = 1;
                    codes.Clear();
                    lines.Clear();
                    textContainer.Clear();
                    loadFile(files[0]);
                    listData();
                }
            }
            updateSaveBtnState();
        }

        private void mainForm_DragEnter(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop) && ((string[])e.Data.GetData(DataFormats.FileDrop))[0].EndsWith(".csv"))
            {
                e.Effect = DragDropEffects.Link;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
    }
}
