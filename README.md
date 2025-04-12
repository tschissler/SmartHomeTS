# SmartHomeTS - A Comprehensive Playground for Smart Home Technologies

This repository is a collection of tools and projects I have created to build and enhance my SmartHome system. It serves as a playground to experiment with and learn about various technologies while also being used in production to observe real-world pros, cons, and issues over time.

## Purpose

The primary goal of this repository is to:
- Optimize energy consumption through automation and visualization.
- Add useful and fun gadgets to enhance the smart home experience.
- Experiment with and learn about diverse technologies in a practical setting.
- Test software in production to identify and address long-term issues.

While the code is tailored specifically to my equipment, devices, and needs, it is shared here to inspire others. Feel free to explore, copy, and adapt anything you find useful.

## Technologies Used

This repository showcases a variety of cutting-edge technologies, each solving specific problems and offering opportunities for learning:

- **.NET**: A powerful framework for building scalable backend services. In this repository, it powers projects like ChargingController and DataAggregator, demonstrating how to create high-performance applications with strong type safety and modularity. Learn more at [.NET Documentation](https://learn.microsoft.com/en-us/dotnet/).
- **Python**: Known for its simplicity and versatility, Python is used in the BMWConnector to integrate with BMW/Mini Connected services and VWConnector to consume the VW Connect API. As there are libraries available to provide the necessary interfaces, these projects demonstrate how to use them and create MQTT messages based on the data. Learn more at [Python.org](https://www.python.org/).
- **C/C++**: Essential for low-level programming, these languages are used in ESP32 firmware to control IoT devices like sensors and actuators. This is a great example of how to build efficient and reliable embedded systems. Learn more at [C++ Documentation](https://cplusplus.com/) and [C Programming](https://en.cppreference.com/w/).
- **PlatformIO**: A professional collaborative platform for embedded development fully integrated in VS Code. It is used in the ESP32Firmwares projects to simplify the development, deployment (flashing) of firmware for IoT devices. Learn more at [PlatformIO](https://platformio.org/).
- **MQTT**: A lightweight messaging protocol ideal for IoT. It enables seamless communication between devices and services, as seen in projects like MQTTBroker and ShellyConnector. Learn more at [MQTT.org](https://mqtt.org/).
- **InfluxDB**: A time-series database optimized for storing and querying time-stamped data. This repository uses InfluxDB to manage energy consumption and climate metrics, showcasing its power in data analytics. Learn more at [InfluxDB Documentation](https://docs.influxdata.com/).
- **Kubernetes**: A container orchestration platform that ensures scalability and reliability. Kubernetes is used across multiple projects to manage containerized applications, making it a must-learn for modern DevOps. Learn more at [Kubernetes.io](https://kubernetes.io/). In this project I uses [microk8s](https://microk8s.io/) to host a kubernetes cluster on a set of Raspberry Pis. 
- **Docker**: Simplifies application deployment by containerizing services. Container images are build during the build process and then used to deploy to the kubernetes cluster. Learn more at [Docker Documentation](https://docs.docker.com/).
- **Flutter**: A cross-platform framework for building mobile apps. The Smarthome.App.MAUI project illustrates how to create a unified user experience across devices. Learn more at [Flutter.dev](https://flutter.dev/).
- **Blazor**: A web framework for building interactive applications using C#. The SmartHome.Web project is a great example of how to create responsive and feature-rich web interfaces. Learn more at [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/).
- **Syncfusion**: Provides UI components for creating interactive and visually appealing applications. Used in SmartHome.Web, it demonstrates how to enhance user interfaces with minimal effort. Learn more at [Syncfusion Blazor Components](https://www.syncfusion.com/blazor-components).

## Projects Overview

### 3DModels
- **Purpose**: Contains 3D models for sensor cases and module boxes, enabling the physical integration of IoT devices into the smart home system.
- **Inspiration**: Learn how to design and 3D print custom enclosures for your IoT projects.

### BMWConnector
- **Purpose**: Integrates with BMW/Mini Connected services to fetch vehicle data, such as battery status and charging information, and publishes it as MQTT messages.
- **Inspiration**: Explore how to use Python for API integration and real-time data processing.

### ChargingController
- **Purpose**: Manages electric vehicle charging processes, optimizing energy usage based on availability and demand.
- **Inspiration**: Learn how to build energy-efficient systems using .NET and MQTT.

### DataAggregator
- **Purpose**: Aggregates and processes data from various sources, providing a unified view of the smart home system.
- **Inspiration**: Discover how to create modular and testable data processing pipelines.

### EnphaseConnector
- **Purpose**: Connects to Enphase solar systems to retrieve live data and control solar energy usage.
- **Inspiration**: Understand how to integrate renewable energy systems into a smart home.

### ESP32Firmwares
- **Purpose**: Firmware for ESP32-based devices, including LED strips, temperature sensors, and more.
- **Inspiration**: Learn how to program microcontrollers for IoT applications.

### HomeAssistant
- **Purpose**: Configuration for Home Assistant, enabling integration and automation of various smart home devices.
- **Inspiration**: Explore how to create a centralized automation system for your home.

### Influx
- **Purpose**: Manages InfluxDB for storing and querying time-series data, such as energy consumption and climate metrics.
- **Inspiration**: See how to use time-series databases for advanced data analytics.

### MQTTBroker
- **Purpose**: Sets up an MQTT broker for communication between devices and services.
- **Inspiration**: Learn how to build a reliable messaging system for IoT.

### Smarthome.App.MAUI
- **Purpose**: A cross-platform mobile app for managing the SmartHome system, providing a user-friendly interface for monitoring and control.
- **Inspiration**: Discover how to create mobile apps with real-time data visualization.

### SmartHome.Web
- **Purpose**: A web interface for visualizing and controlling the SmartHome system, offering a centralized dashboard.
- **Inspiration**: Learn how to build interactive web applications with Blazor and Syncfusion.

### ShellyConnector
- **Purpose**: Integrates with Shelly smart devices, enabling control and monitoring of connected appliances.
- **Inspiration**: Explore how to connect and manage smart devices using MQTT.

## Tools Frequently Used

While working with this repository, the following tools are frequently utilized to enhance productivity and streamline development:

- **VSCode**: A lightweight and versatile code editor, ideal for quick edits and exploring the codebase. Extensions like PlatformIO and Docker make it a powerful tool for embedded and containerized development.
- **Visual Studio**: Used for developing and debugging .NET projects like ChargingController and DataAggregator. Its robust debugging tools and integration with Azure DevOps make it indispensable for large-scale projects.
- **MQTT Explorer**: A graphical MQTT client that simplifies the process of monitoring and debugging MQTT topics and messages. It is particularly useful for projects like MQTTBroker and ShellyConnector.

## Kubernetes Secrets Usage

Kubernetes secrets are used extensively in this repository to securely manage sensitive information such as API keys, credentials, and certificates. Here’s how they are typically utilized:

- **Configuration**: Secrets are defined in Kubernetes YAML files and mounted as environment variables or files in the respective pods.
- **Integration**: Projects like MQTTBroker and HomeAssistant use secrets to store sensitive configuration data, ensuring secure communication and operation.
- **Best Practices**: Secrets are encrypted at rest and access is restricted to only the services that require them, following Kubernetes security best practices.

## Unique Patterns and Practices

This repository showcases several unique patterns and practices that enhance its functionality and maintainability:

- **MQTT Integration**: The `MQTTService` class in multiple projects handles MQTT messages by subscribing to topics and updating shared data models. This demonstrates a robust approach to real-time communication in IoT systems.

- **ESP32 Firmware Patterns**:
  - **Circular Buffers**: Efficiently handle serial data streams in firmware projects like `SMLSensor.Firmware`.
  - **Custom Protocol Parsing**: The `SMLParser` class extracts and processes data packets using start and end sequences, ensuring reliable data handling.
  - **OTA Updates**: Over-the-air update mechanisms are implemented for seamless firmware upgrades.

- **Data Parsing and Validation**: The `SMLParser` class uses structured methods to parse binary data into meaningful elements, with robust error handling for edge cases.

- **IoT Device Communication**: Projects like `PowerDogConnector` and `KellerDevice` demonstrate effective patterns for interacting with IoT devices, including sensor data retrieval and MQTT publishing.

- **Cross-Platform Development**: The `Smarthome.App.MAUI` project leverages .NET MAUI for creating cross-platform mobile applications with a unified user experience.

- **Azure Integration**: Some firmware projects include Azure root certificates, indicating secure communication with Azure IoT services.

- **Error Handling and Logging**: Consistent emphasis on error handling and logging across the codebase aids in debugging and ensures system reliability.

## External Resources

- [bimmer_connected Documentation](https://bimmer-connected.readthedocs.io/): For BMW/Mini integration.
- [Home Assistant](https://www.home-assistant.io/): Smart home automation platform.
- [Syncfusion Blazor Components](https://www.syncfusion.com/blazor-components): UI components for Blazor.
- [InfluxDB Documentation](https://docs.influxdata.com/): For time-series data management.
- [MQTT Protocol](https://mqtt.org/): Lightweight messaging protocol for IoT.
- [PlatformIO Documentation](https://docs.platformio.org/): Embedded development platform.

This repository is a work in progress and is tailored to my specific needs. However, it is shared with the hope that it provides inspiration and learning opportunities for others interested in SmartHome technologies.
