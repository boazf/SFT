using System;
using System.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SFT
{
    public class SyntaxException : Exception
    {
        public SyntaxException(string message) : 
            base(message)
        {
        }
    }

    class Client
    {
        private string m_address = null;
        private TcpClient m_client = null;
        private const int port = 765;

        public Client(string address)
        {
            m_address = address;
            OpenClient();
        }

        string Address
        {
            get
            {
                return m_address;
            }
        }

        private void OpenClient()
        {
            if (m_client != null)
                m_client.Close();
            m_client = new TcpClient();

            try
            {
                m_client.Connect(m_address, port);
            }
            catch (Exception)
            { 
                m_client.Close();
                m_client = null;
                throw;
            }

            m_client.SendTimeout = 5000;
            m_client.ReceiveTimeout = 5000;
            m_client.ReceiveBufferSize = 256;
            m_client.SendBufferSize = 256;
        }

        private byte[] UnsafeSendReceive(byte[] b)
        {
            NetworkStream nwStream = m_client.GetStream();
            nwStream.Write(b, 0, b.Length);
            return Receive(nwStream);
        }

        private byte[] Receive(NetworkStream nwStream)
        {
            byte[] bytesToRead = new byte[m_client.ReceiveBufferSize];
            int bytesRead = nwStream.Read(bytesToRead, 0, bytesToRead.Length);
            return bytesToRead.Take(bytesRead).ToArray();
        }

        public byte[] Receive()
        {
            NetworkStream nwStream = m_client.GetStream();
            return Receive(nwStream);
        }

        private byte[] RetrySendReceive(byte[] bytesToSend)
        {
            OpenClient();

            return UnsafeSendReceive(bytesToSend);
        }

        public byte[] SendReceive(byte[] bytesToSend)
        {
            byte[] response;

            try
            {
                response = UnsafeSendReceive(bytesToSend);
                if (response.Length == 0)
                {
                    response = RetrySendReceive(bytesToSend);
                }
            }
            catch (SocketException)
            {
                response = RetrySendReceive(bytesToSend);
            }
            catch (IOException)
            {
                response = RetrySendReceive(bytesToSend);
            }

            return response;
        }

        public string SendReceive(string textToSend)
        {
            byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(textToSend);
            byte[] bytesReceived = SendReceive(bytesToSend);
            return Encoding.ASCII.GetString(bytesReceived, 0, bytesReceived.Length);
        }

        public void Disconnect()
        {
            if (m_client != null)
                m_client.Close();
        }
    }

    class SFTServer
    {
        private Client m_client = null;
        private string[] m_args;
        private byte m_major;
        private byte m_minor;

        public SFTServer(string[] args)
        {
            m_args = args;
        }

        private void Connect(string param)
        {
            if (m_client != null)
            {
                throw new Exception("Connection is already opened");
            }

            if (string.IsNullOrEmpty(param))
            {
                throw new SyntaxException("Missing address");
            }
            try
            {
                m_client = new Client(param);
                byte[] reply = m_client.SendReceive(new byte[] { (byte)'C' });
                if (reply.Length == 2)
                {
                    m_major = reply[1];
                    m_minor = reply[0];
                    Console.WriteLine($"Connected, server version: { m_major }.{ m_minor }");
                }
                else
                {
                    throw new Exception($"Unexpected reply from connect command");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to { param }", ex);
            }
        }

        private void Disconnect()
        {
            if (m_client == null)
                return;
            m_client.Disconnect();
            m_client = null;
        }

        private void VerifyConnected()
        {
            if (m_client == null)
                throw new Exception("not conneted to server");
        }

        private string ParsePath(string param, out string suffix)
        {
            string path;

            if (param[0] == '\"')
            {
                path = param.Substring(1);
                int second = path.IndexOf('\"');
                if (second == -1 || second == path.Length - 1)
                {
                    suffix = "";
                    return path;
                }
                else
                {
                    suffix = path.Substring(second + 1);
                    path = path.Substring(0, second);
                    if (suffix[0] != ' ')
                    {
                        throw new SyntaxException("first path argument is not valid");
                    }
                    suffix = suffix.Trim(' ');
                }
            }
            else
            {
                int space = param.IndexOf(' ');
                if (space != -1)
                {
                    path = param.Substring(0, space);
                    suffix = param.Substring(space + 1).Trim(' ');
                }
                else
                {
                    suffix = "";
                    path = param;
                }
            }
            return path;
        }

        private void CreateRemoteFileForUpload(string path, long len)
        {
            byte[] reply = m_client.SendReceive((new byte[] { (byte)'U' }).Concat(Encoding.ASCII.GetBytes(path)).Concat(new byte[] { 0 }).Concat(BitConverter.GetBytes(len)).ToArray());
            if (reply.Length != 1)
                throw new Exception("unexpected Arduino reply upon destination file creation");
            else if (reply[0] != 220)
                throw new Exception($"failed to create destination file on Arduino, reply={ reply[0] }");
        }

        private const int transferBufferSize = 256;

        private void Upload(string param)
        {
            VerifyConnected();

            string to;
            string from = ParsePath(param, out to);
            using (FileStream source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.None, transferBufferSize))
            {
                if (string.IsNullOrEmpty(to))
                {
                    to = Path.GetFileName(from);
                }
                byte[] buffer = new byte[transferBufferSize];
                CreateRemoteFileForUpload(to, source.Length);
                int offset = 0;
                do
                {
                    int n = source.Read(buffer, 0, transferBufferSize);
                    byte[] reply = m_client.SendReceive(buffer.Take(n).ToArray());
                    if (reply.Length != 1)
                        throw new Exception("unexpected Arduino reply upon destination file transmission");
                    else if (reply[0] != 220)
                        throw new Exception($"failed to write destination file to Arduino, reply={ reply[0] }");
                    offset += n;
                } while (offset < source.Length);
            }
            byte[] finalReply = m_client.SendReceive(new byte[] { (byte)'u' });
            if (finalReply.Length != 1 || finalReply[0] != 220)
                throw new Exception("unexpected Arduino reply upon destination file transmission end");
        }

        void Download(string param)
        {
            VerifyConnected();

            string to;
            string from = ParsePath(param, out to);

            byte[] reply = m_client.SendReceive(ASCIIEncoding.ASCII.GetBytes('W' + from));
            if (reply[0] != 220)
            {
                throw new Exception("operation failed, probably source file does not exist");
            }

            if (reply.Length != 5)
            {
                throw new Exception($"Unexpected reply upon download request. Received buffer size: { reply.Length }, expected: 5");
            }

            int length = reply[1] | (reply[2] << 8) | (reply[3] << 16) | (reply[4] << 24);

            if (string.IsNullOrEmpty(to))
            {
                to = Path.GetFileName(from);
            }

            using (FileStream dest = new FileStream(to, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, transferBufferSize))
            {
                dest.SetLength(0);
                int offset = 0;
                do
                {
                    reply = m_client.SendReceive(new byte[] { (byte)'w' });
                    if (reply.Length == 0)
                    {
                        dest.Close();
                        File.Delete(to);
                        throw new Exception($"Failed to download file { to }");
                    }
                    dest.Write(reply, 0, reply.Length);
                    offset += reply.Length;
                } while (offset < length);
            }

            reply = m_client.SendReceive(new byte[] { (byte)'?' });
            if (reply.Length != 1 || reply[0] != 220)
                throw new Exception("unexpected Arduino reply upon destination file transmission end");
        }

        class DirectoryEntry
        {
            public DirectoryEntry(byte[] bytes)
            {
                FileDateTime = new DateTime(bytes[4] | bytes[5] << 8, bytes[3], bytes[2], bytes[6], bytes[7], 0);
                Size = bytes[8] | (bytes[9] << 8) | (bytes[10] << 16) | (bytes[11] << 24);
                IsDir = bytes[12] == 1;
                int len = bytes.Length - 256;
                for (int i = 13; i < bytes.Length; i++)
                    if (bytes[i] == 0)
                    {
                        len = i - 13;
                        break;
                    }
                Name = Encoding.ASCII.GetString(bytes, 13, len);
            }

            public DateTime FileDateTime { get; private set; }

            public long Size { get; private set; }

            public string Name { get; private set; }

            public bool IsDir { get; private set; }
        }

        private void ListDirectory()
        {
            VerifyConnected();

            byte[] reply = m_client.SendReceive(new byte[] { (byte)'L' });

            while (reply[0] == 222)
            {
                DirectoryEntry de = new DirectoryEntry(reply);
                string fileSize = de.IsDir ? "" : de.Size.ToString("N0", CultureInfo.InvariantCulture);
                string dir = de.IsDir ? "<DIR>" : "";
                Console.WriteLine($"{de.FileDateTime.Day:00}/{de.FileDateTime.Month:00}/{de.FileDateTime.Year} {de.FileDateTime.Hour:00}:{de.FileDateTime.Minute:00}{fileSize,15}{dir,7} {de.Name}");
                reply = m_client.SendReceive(new byte[] { (byte)'l' });
            }
            if (reply[0] != 220)
                throw new Exception("Unexpected reply upon end of list directory");
        }

        private void ChangeDirectory(string subDir)
        {
            VerifyConnected();

            byte[] reply = m_client.SendReceive(ASCIIEncoding.ASCII.GetBytes('D' + subDir));
            if (reply[0] != 220)
                Console.Error.WriteLine($"Operation failed");
            Console.WriteLine($"Current directory: { Encoding.ASCII.GetString(reply, 1, reply.Length - 1)}");
        }

        private void MakeDirectory(string subDir)
        {
            VerifyConnected();

            byte[] reply = m_client.SendReceive(ASCIIEncoding.ASCII.GetBytes('M' + subDir));
            if (reply[0] != 220)
                Console.Error.WriteLine($"Operation failed");
        }

        private void Delete(string fileName)
        {
            VerifyConnected();

            byte[] reply = m_client.SendReceive(ASCIIEncoding.ASCII.GetBytes('x' + fileName));
            if (reply[0] != 220)
                Console.Error.WriteLine($"Operation failed");
        }

        private void DeleteDirectory(string subDir)
        {
            VerifyConnected();

            byte[] reply = m_client.SendReceive(ASCIIEncoding.ASCII.GetBytes('X' + subDir));
            if (reply[0] != 220)
                Console.Error.WriteLine($"Operation failed");
        }

        private void ChangeLocalDirectory(string param)
        {
            if (!string.IsNullOrEmpty(param))
                Directory.SetCurrentDirectory(param);
            Console.WriteLine($"Current local directory: { Directory.GetCurrentDirectory() }");
        }

        private void LocalSystemCommand(string cmd)
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + cmd);
            psi.CreateNoWindow = false;
            psi.UseShellExecute = false;
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.Start();
                p.WaitForExit();
            }
        }

        private bool DoCMD(string cmd)
        {
            try
            {
                string param;
                string cmdType = ParseCMD(cmd, out param);
                return ExecCMD(cmdType, param);
            }
            catch (SyntaxException ex)
            {
                Console.Error.WriteLine($"Bad command syntax: { ex.Message }");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: { ex.Message }");
            }

            return true;
        }

        private string ParseCMD(string cmd, out string param)
        {
            int space = cmd.IndexOf(' ');
            if (space == -1)
            {
                param = "";
                return cmd.ToUpper(CultureInfo.InvariantCulture);
            }

            param = cmd.Substring(space + 1).Trim(' ');

            return cmd.Substring(0, space).ToUpper(CultureInfo.InvariantCulture);
        }

        private void CommandHelp(string command, string explanation)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{command} - ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(explanation);
        }

        private void Help()
        {
            ConsoleColor prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("List of Commands (case insensitive):");
            CommandHelp("{ CONNECT | OPEN } IPAddress", "Connect to server with specified IP address");
            CommandHelp("{ DISCONNECT | DISCO }", "Disconnect from server.");
            CommandHelp("{ UPLOAD | UP } SrcPath [ DstFile ]", "Upload a file from the client to the server. SrcPath is the file path to the source file to be uploaded. If there are spaces in SrcPath, it should have the \" character at the beginnig and the end of it. DstFile is optional file name for the destination file. By default the destination file name is taken from SrcPath. The file is always uploaded to the current directory on the server. It is not possible to specify file path in DstFile.");
            CommandHelp("DOWNLOAD SrcFile [ DstPath ]", "Download a file from the server to the client. The downloaded file should exist in the current directory of the server. It is not possible to specify path in SrcFile. DstPath is optional path to the destination file. By default the file is downloaded to the local current directory on the client with the same name.");
            CommandHelp("DIR", "Prints the content of the current directory on the server.");
            CommandHelp("CD [ DirName ]", "Changes the current directory on the server. DirName is oprtional. If DirName is not specified, the directory remains the same and printed on the console. DirName cannot contain path, only the name of one directory, or .. to change to one directory level up.");
            CommandHelp("{ MKDIR | MD } DirName", "Create a new directory in the current directory on the server. DirName cannot contain path, only directory name.");
            CommandHelp("{ RMDIR | RD } DirName", "Remove a directory from the current directory on the server. DirName cannot contain path, only directory name. The directory should be empty or the operation will fail.");
            CommandHelp("DEL FileName", "Delete a file from the current directory on the server. Filename cannot contain path, only file name.");
            CommandHelp("LCD [ Path ]", "Change curernt local directory on the client. The path is optional. If it is missing then the current local directory on the client is printed to the console.");
            CommandHelp("EXIT", "Terminate application");
            CommandHelp("{ HELP | ? }", "Print this help message.");
            CommandHelp("!command", "Executes the specified command on the client.");
            Console.ForegroundColor = prevColor;
        }

        private bool ExecCMD(string cmd, string param)
        {
            switch (cmd)
            {
                case "CONNECT":
                case "OPEN":
                    Connect(param);
                    break;

                case "DISCONNECT":
                case "DISCO":
                    Disconnect();
                    break;

                case "UPLOAD":
                case "UP":
                    Upload(param);
                    break;

                case "DOWNLOAD":
                    Download(param);
                    break;

                case "DIR":
                    ListDirectory();
                    break;

                case "CD":
                    ChangeDirectory(param);
                    break;

                case "MKDIR":
                case "MD":
                    MakeDirectory(param);
                    break;

                case "DELETE":
                case "DEL":
                    Delete(param);
                    break;

                case "RMDIR":
                case "RD":
                    DeleteDirectory(param);
                    break;

                case "LCD":
                    ChangeLocalDirectory(param);
                    break;

                case "EXIT":
                    Disconnect();
                    return false;

                case "HELP":
                case "?":
                    Help();
                    break;

                case "":
                    break;

                default:
                    if (cmd[0] == '!')
                        LocalSystemCommand(cmd.Substring(1) + " " + param.Trim(' '));
                    else
                        throw new SyntaxException($"Unknown command: { cmd }");
                    break;
            }
            return true;
        }

        public void ExecLoop()
        {
            do
            {
                Console.Write("sft >");
            } while (DoCMD(Console.ReadLine()));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SFTServer server = new SFTServer(args);

            server.ExecLoop();
        }
    }
}
