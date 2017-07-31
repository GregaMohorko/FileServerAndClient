# File Server and Client
A console file server and client application. It uses TCP protocol and RSA encryption.

Supports downloading/uploading/deleting files, creating/deleting directories, listing files & directories on the server and getting the server time.

## Screenshots

### Server
![Server application](/Documentation/Screenshots/Screenshot_Server.png?raw=true "Server application")

You just write in the desired port, and the server starts listening.

The log can be used to monitor what is happening. It logs the request of every connected client. It also logs any errors that happened when serving the clients (i.e. non-existent directory).

### Client
![Client application](/Documentation/Screenshots/Screenshot_Client.png?raw=true "Client application")

You can get a list of files and directories in a specific directory on the server. To get the root content, use '/'.

![Listing files and directories](/Documentation/Screenshots/Screenshot_Client_Listing.png?raw=true "Listing files and directories")

To **download** a file, choose option *3*. Then you have to type in the file path on the server and choose where to download it. The encrypted content of the file is downloaded to a temporary file, and then decrypted.

![Downloading a file](/Documentation/Screenshots/Screenshot_Client_Downloading.png?raw=true "Downloading a file")

Of course, you don't have to write the client file paths by hand because there is a handy windows **file dialog**.

![File dialog](/Documentation/Screenshots/Screenshot_Client_Uploading_Dialog.png?raw=true "File dialog")

In case you misspell the server path or any other invalid operation occurs, you get some feedback of what went wrong.

![Error handling](/Documentation/Screenshots/Screenshot_Client_ErrorHandling.png?raw=true "Error handling")

## Requirements
.NET Framework 4.6.1

## Author and License
Grega Mohorko ([www.mohorko.info](http://www.mohorko.info))

Copyright (c) 2013 Grega Mohorko

[MIT License](./LICENSE)
