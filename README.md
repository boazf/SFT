# SFT - Simple File Transfer
This application is meant to be used with Arduino Internet Recovery Box (IWG) project.
Arduino Internet Recovery Box has an SD card that holds configuration parameters and various files
that are used in order to present the web (HTML) interface of the application. The SFT applicatinon
is used in order to transfer files between the SD card on the Arduino (called the server) and the PC where 
the SFT application is running (called the client). This application saves the need to extract the SD card from the Arduino, 
stick it in an SD card reader in the PC, transfter the files and then stick it back in the Arduino card reader.</br></br>
The application works as a very simple command shell. The available commands are as follows:</br></br>
**{ CONNECT | OPEN } IPAddress** - Connect to server with specified IP address.</br>
**{ DISCONNECT | DISCO }** - Disconnect from server.</br>
**{ UPLOAD | UP } SrcPath [ DstFile ]** - Upload a file from the client to the server. SrcPath is the file path to the source file to be uploaded. If there are spaces in SrcPath, it should have the " character at the beginnig and the end of it. DstFile is optional file name for the destination file. By default the destination file name is taken from SrcPath. The file is always uploaded to the current directory on the server. It is not possible to specify file path in DstFile.</br>
**DOWNLOAD SrcFile [ DstPath ]** - Download a file from the server to the client. The downloaded file should exist in the current directory of the server. It is not possible to specify path in SrcFile. DstPath is optional path to the destination file. By default the file is downloaded to the local current directory on the client with the same name.</br>
**DIR** - Prints the content of the current directory on the server.</br>
**CD [ DirName ]** - Changes the current directory on the server. DirName is oprtional. If DirName is not specified, the directory remains the same and printed on the console. DirName cannot contain path, only the name of one directory, or .. to change to one directory level up.</br>
**{ MKDIR | MD } DirName** - Create a new directory in the current directory on the server. DirName cannot contain path, only directory name.</br>
**{ RMDIR | RD } DirName** - Remove a directory from the current directory on the server. DirName cannot contain path, only directory name. The directory should be empty or the operation will fail.</br>
**DEL FileName** - Delete a file from the current directory on the server. Filename cannot contain path, only file name.</br>
**LCD [ Path ]** - Change curernt local directory on the client. The path is optional. If it is missing then the current local directory on the client is printed to the console.</br>
**EXIT** - Terminate application</br>
**{ HELP | ? }** - Print this help message.</br>
**!command** - Executes the specified command on the client.</br>

