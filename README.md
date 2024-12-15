# File Upload Server

**Runs on:** [http://localhost:7140](http://localhost:7140)

- **Access:** Should be accessible on any device within the same network as the PC running this application.
- **Required .NET Version:** This server requires .NET 8.0 to run.
- **Known Issues:** The server may have memory problems. **Note:** No further fixes for these issues will be provided.
- **Usage Recommendations:** 
  - **Do not expose this server to the public internet.** It is not secure for use outside of a controlled, private network.

### Features
- **File Upload:** The server allows users to upload files through a simple web interface.
- **Shutdown Capability:** Users can shut down the server via a dedicated button on the webpage.

### How to Use
1. **Run the Server:**
   - Ensure you have .NET 8.0 installed on your machine.
   - Navigate to the project directory and run:
     ```bash
     dotnet run
     ```

2. **Accessing the Server:**
   - The server will start and listen on `http://localhost:7140`.
   - To upload files, visit the address in your web browser.
   
3. **Features:**
   - **File Upload:** You can use the file input field to select and upload files.
   - **Shutdown Button:** To shut down the server, click the "Shutdown Server" button on the main page.

### Notes
- **Self-contained deployment:** You may need to bundle .NET Core with your executable for ease of distribution (refer to the .NET documentation for instructions).
- **Security Warning:** This application should only be used in a private, controlled network environment. It is not intended for use on public networks or in production settings.
