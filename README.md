# EasyWire

EasyWire is a user-friendly WireGuard VPN management system with a web interface. It simplifies the process of setting up and managing WireGuard VPN configurations.

## Features

- Web-based interface for managing WireGuard configurations
- Easy creation and management of client configurations
- QR code generation for quick mobile device setup
- Real-time status updates for client connections
- Dockerized for easy deployment

## Quick Start

1. Install Docker and Docker Compose:

   - For Ubuntu/Debian:
     ```
     sudo apt update && sudo apt install docker-compose
     ```

2. Copy the docker-compose.yml file to your server:
   ```
   wget https://raw.githubusercontent.com/killyp/EasyWire/master/docker-compose.yml
   ```

3. Edit the `docker-compose.yml` file:
   - Set the `HOST` environment variable to your server's public IP address or domain name.
   - Set the `PASSWORD` environment variable to a secure password of your choice.

4. Start the EasyWire container:
   ```
   sudo docker-compose pull && sudo docker-compose up -d
   ```

5. Access the web interface at `http://your-server-ip`.

## Usage

1. Log in using the password you set in the `docker-compose.yml` file.
2. Click "New Configuration" to create a new client configuration.
3. Use the generated QR code or configuration file to set up your client devices.
4. Manage existing configurations using the web interface.

## Development

EasyWire is built using:
- Blazor Server and .NET 8.0
- MudBlazor is used for UI components

To set up a development environment:

1. Install the .NET 8 SDK
2. Install Docker Desktop
3. Clone the repository
4. Open the solution in your preferred IDE
5. Run the application using `docker-compose-dev.yml`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

[MIT License](LICENSE)

## Acknowledgements

- [WireGuard](https://www.wireguard.com/)
- [MudBlazor](https://mudblazor.com/)
