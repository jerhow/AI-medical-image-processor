# AI-Powered Medical Image Processor

[![Build & Deploy Status](https://github.com/jerhow/AI-medical-image-processor/actions/workflows/dotnet.yml/badge.svg)](https://github.com/jerhow/AI-medical-image-processor/actions)
This project is a web application designed to demonstrate the integration of various Azure AI services for medical image analysis. Users can upload medical images (currently chest X-rays) which are then processed asynchronously for AI-powered insights, including image classification, optical character recognition (OCR), and object detection. The system features a secure API, a responsive web frontend, and a full CI/CD pipeline for automated deployment to Azure.

## Key Features

* **Secure Image Upload:** Supports common image formats (PNG, JPG, JPEG, BMP) for analysis.
* **AI-Powered Image Classification:** Classifies uploaded X-rays into categories such as "Normal," "Cardiomegaly," "Effusion," and "Pneumothorax" using Azure Custom Vision.
* **AI-Powered Optical Character Recognition (OCR):** Extracts visible text from images using Azure AI Vision.
* **AI-Powered Object Detection:** Identifies and locates specific objects within images (e.g., "Pacemaker") using Azure Custom Vision, displaying bounding boxes and confidence scores.
* **Asynchronous Analysis Workflow:** Image processing tasks are handled asynchronously using an in-process background queue, ensuring the user interface remains responsive.
* **Secure API:** The backend API is protected using API Key authentication.
* **Dynamic Results Display:** The web frontend polls for analysis status and dynamically displays classification predictions, OCR text, and object detection bounding boxes.
* **CSV Report Download:** Allows users to download a comprehensive CSV report of all image analysis jobs and their results.
* **Automated CI/CD Pipeline:** Utilizes GitHub Actions for continuous integration (building and unit testing) and continuous deployment to Azure App Services.

## Live Demo

* **Web Application:** `Link available upon request`
* **API (Swagger UI):** `Link available upon request`

*(Note: This is a portfolio project. To manage costs, the deployed Azure services are not always running. Demo available upon request.)*

## Showcase: Features in Action

This video demonstrates:
* The image upload process.
* The display of classification results.
* The display of extracted OCR text.
* The rendering of object detection bounding boxes on an image.

_Video has sound (unmute to hear)_

https://github.com/user-attachments/assets/1d20674b-0b2a-49a0-b44a-a446e50e7934

This video demonstrates CSV report download functionality:

_Video has sound (unmute to hear)_

https://github.com/user-attachments/assets/d7c11c3b-7997-4eb3-96a6-c211bfdbd52b

## The Use Case / Problem Solved

Medical image review can be a time-consuming process, requiring careful attention from trained professionals. This project explores how AI can be leveraged to assist in this domain by providing preliminary insights and automating certain analytical tasks. 

Key objectives and potential uses include:
* **Demonstrating AI Capabilities:** Showcasing the integration of multiple AI services (classification, OCR, object detection) to extract diverse information from medical images.
* **Workflow Efficiency:** Illustrating how an asynchronous processing backend can handle potentially long-running AI tasks without blocking the user interface, improving user experience.
* **Preliminary Analysis Support:** While not a diagnostic tool, this application can serve as a proof-of-concept for systems that might:
    * Help triage cases by providing initial classifications or highlighting specific objects.
    * Extract textual data from images for record-keeping or further processing.
    * Provide a platform for experimenting with and evaluating different AI models on medical imagery.
* **Educational Tool:** Serving as a comprehensive example of a full-stack .NET application integrated with modern cloud AI services and CI/CD practices.
* **Versatile AI Platform:** While this initial version focuses on chest X-ray analysis (classification, OCR, and pacemaker detection), the underlying architecture is designed for broader applicability. By training new AI models in Azure Custom Vision for different imaging modalities (e.g., MRI, CT scans, dermatology photographs, pathology slides) or for identifying other anomalies and objects, the system can be readily extended to support a wider range of medical image analysis tasks. This demonstrates a flexible framework for integrating various AI-driven image interpretations.

## Leveraging Artificial Intelligence

This project integrates three distinct AI capabilities to analyze medical images, demonstrating a versatile approach to extracting insights:

### 1. Image Classification (Azure Custom Vision)

* **Purpose:** To categorize uploaded chest X-ray images into predefined classes, such as "Normal" (No Finding), "Cardiomegaly," "Effusion," and "Pneumothorax." This provides a high-level assessment of the image.
* **Model & Training:**
    * A custom image classification model was trained using **Azure Custom Vision**.
    * The training process was iterative, starting with an initial dataset sourced from the [NIH Chest X-ray Dataset](https://www.kaggle.com/datasets/nih-chest-xrays/data).
    * Key considerations during training included:
        * **Data Curation:** Selecting appropriate images for each category.
        * **Data Augmentation & Balancing:** Strategies to ensure a sufficient and relatively balanced number of images per tag (e.g., starting with ~1000-1500 images per category for a 4-pathology model).
        * **Iterative Refinement:** Multiple training iterations were performed, analyzing performance metrics (Precision, Recall, Average Precision) and adjusting categories/data to improve model accuracy and reliability.
    * This process demonstrates skills in data preparation for AI, model training with cloud AI services, and iterative model performance evaluation.

### 2. Optical Character Recognition (OCR - Azure AI Vision)

* **Purpose:** To automatically detect and extract any printed or handwritten text visible on the uploaded medical images. This could include patient information (in anonymized datasets), dates, annotations, or device markings.
* **Implementation:** Utilizes the pre-trained **Read API** from **Azure AI Vision service**. This powerful OCR engine handles text extraction without requiring custom model training for this specific feature.
* This demonstrates the ability to integrate general-purpose, pre-trained AI services for specific data extraction tasks.

### 3. Object Detection (Azure Custom Vision)

* **Purpose:** To identify and locate specific objects within the medical images by drawing bounding boxes around them. Currently, this is implemented to detect "Pacemaker" devices in chest X-rays.
* **Model & Training:**
    * A custom object detection model was trained using **Azure Custom Vision**.
    * The training process involved:
        * **Data Collection:** Sourcing images containing the target object (Pacemakers) and a diverse set of negative images (X-rays without pacemakers).
        * **Data Labeling:** Manually drawing precise bounding boxes around each instance of a "Pacemaker" in the training images and tagging them. This is a critical and detailed step for object detection.
        * **Iterative Training:** Training the model and evaluating its performance using object detection metrics like Mean Average Precision (mAP), Precision, and Recall.
        * **Thresholding:** Applying a confidence threshold to the predictions to balance detection accuracy and reduce false positives.
    * This feature showcases skills in preparing and labeling data for object detection, training specialized detector models, and processing bounding box outputs.

## Technology Stack

This project utilizes a modern, cloud-native technology stack:

* **Backend:**
    * C# 12 / .NET 8
    * ASP.NET Core 8 Web API (for RESTful services)
    * Entity Framework Core 8 (for database interaction with Azure SQL)
* **Frontend:**
    * ASP.NET Core 8 Razor Pages
    * HTML5, CSS3, JavaScript (ES6+)
    * Bootstrap 5 (for responsive UI components)
* **Artificial Intelligence (Azure AI):**
    * **Azure Custom Vision:** For training and hosting custom Image Classification and Object Detection models.
    * **Azure AI Vision (formerly Computer Vision):** For Optical Character Recognition (OCR) using the Read API.
* **Database:**
    * Azure SQL Database
* **Storage:**
    * Azure Blob Storage (for secure storage of uploaded medical images)
* **Asynchronous Processing:**
    * .NET `IHostedService` with `System.Threading.Channels` (for robust in-process background task queuing and execution)
* **API Security:**
    * API Key Authentication (implemented via custom ASP.NET Core Middleware)
* **CI/CD & Hosting:**
    * **GitHub Actions:** For automated Continuous Integration (build, unit tests) and Continuous Deployment.
    * **Azure App Service (Linux):** For hosting both the Web API and the Razor Pages Web Application.
* **Unit Testing:**
    * xUnit (as the testing framework)
    * Moq (as the mocking library)

## Architecture Overview

This application is built using a decoupled, service-oriented architecture designed for clarity, maintainability, and to efficiently handle potentially long-running AI processing tasks. The primary components include a user-facing Web Application, a backend API Application that orchestrates AI tasks, and various Azure cloud services for AI capabilities, data storage, and hosting.

![aimip_architecture_diagram](https://github.com/user-attachments/assets/8f1b71de-83f0-4230-bdd0-b1a1cde0c7e5)

### Core Application Components

* **Web Application (`MedicalImageAI.Web` - ASP.NET Core Razor Pages):**
    * Provides the user interface (UI) for image uploads and viewing analysis results.
    * Handles user interaction and makes calls to the backend API.
    * For features like status polling for asynchronous tasks, it uses server-side proxy handlers to securely communicate with the API, keeping sensitive credentials (like API keys) off the client-side.

* **API Application (`MedicalImageAI.Api` - ASP.NET Core Web API):**
    * The central hub for all backend logic and processing.
    * Exposes RESTful endpoints for image uploads, status checks, and report generation.
    * Secured using an API Key authentication mechanism (custom middleware).
    * Orchestrates calls to various Azure AI services and manages data persistence.

* **Service Layer (within API):**
    * `IBlobStorageService`: Encapsulates all interactions with Azure Blob Storage (uploading images, generating SAS URIs for AI service access).
    * `ICustomVisionService`: Handles communication with Azure Custom Vision for both image classification and object detection models.
    * `IOcrService`: Manages calls to Azure AI Vision for Optical Character Recognition (OCR).

* **Asynchronous Processing Backbone**
    * **`IBackgroundQueue<T>` & `BackgroundQueue<T>`:** A custom, in-process, thread-safe queue (built using `System.Threading.Channels`) responsible for holding analysis tasks (delegates) after an image is uploaded. This allows the API to respond quickly to the user.
    * **`QueuedHostedService` (`IHostedService`):** A background worker service that runs continuously within the API application. It dequeues tasks from the `BackgroundQueue` and executes them (e.g., calling the AI services and updating the database) independently of the initial HTTP request lifecycle. This ensures that time-consuming AI operations do not block API responsiveness.

* **Data Persistence (Entity Framework Core):**
    * `ApplicationDbContext`: Manages interactions with the Azure SQL Database.
    * `ImageAnalysisJob` Entity: Stores metadata about each uploaded image, its processing status, and the serialized AI analysis results (classification, OCR, object detection).

### Azure Resource Utilization

The project leverages several key Azure cloud services:

* **Azure App Service (Linux):**
    * Two instances are used: one for hosting the `MedicalImageAI.Api` (backend) and another for the `MedicalImageAI.Web` (frontend). This provides separation and independent scaling if needed.
* **Azure Blob Storage:**
    * Used for durable and scalable storage of all uploaded medical images. Images are stored as private blobs.
* **Azure SQL Database:**
    * A relational database service used to store metadata for each image analysis job, including its status, timestamps, and the JSON-serialized results from the AI analyses.
* **Azure Custom Vision:**
    * Used to train, host, and query two types of custom AI models:
        * An **Image Classification** model (e.g., to identify Normal, Cardiomegaly, Effusion, Pneumothorax).
        * An **Object Detection** model (e.g., to locate Pacemakers).
* **Azure AI Vision (formerly Computer Vision):**
    * The **Read API** is used for its powerful Optical Character Recognition (OCR) capabilities to extract text from images.
* **(Implicitly) Azure Active Directory (Microsoft Entra ID):** Used for creating the Service Principal and Federated Credentials that enable secure, passwordless authentication for the GitHub Actions CI/CD pipeline to deploy resources to Azure.

### Architectural Flow (Example: Image Analysis)

1.  User uploads an image via the **Web App**.
2.  The Web App forwards the image to the **API App** (`/upload` endpoint).
3.  The API's `ImagesController`:
    * Validates the image.
    * Calls `IBlobStorageService` to upload the image to **Azure Blob Storage**.
    * Creates an `ImageAnalysisJob` record in **Azure SQL Database** with "Queued" status and gets a Job ID.
    * Generates a short-lived SAS URI for the uploaded blob.
    * Enqueues a background task (containing the Job ID and SAS URI) into the `IBackgroundQueue`.
    * Immediately returns a `202 Accepted` response to the Web App with the Job ID.
4.  The Web App's UI receives the Job ID and starts polling a status endpoint (via its backend proxy).
5.  The `QueuedHostedService` (running in the API background):
    * Dequeues the task.
    * Updates the job status to "Processing" in the database.
    * Calls `ICustomVisionService` (for classification & object detection) and `IOcrService`, passing the blob's SAS URI. These services interact with **Azure Custom Vision** and **Azure AI Vision**.
    * Receives results, combines them, and updates the `ImageAnalysisJob` record in **Azure SQL Database** with the results and "Completed" status.
6.  The Web App's polling mechanism eventually retrieves the "Completed" status and full analysis results (including bounding boxes) from the API's status endpoint and displays them.

## CI/CD Pipeline (GitHub Actions)

This project utilizes a Continuous Integration and Continuous Deployment (CI/CD) pipeline implemented with **GitHub Actions**.

* **Trigger:** Automatically on pushes/merges to the `main` branch.
* **Build Job:**
    1.  Checks out source code.
    2.  Sets up .NET SDK.
    3.  Restores dependencies.
    4.  Builds the solution (`Release` configuration).
    5.  Runs unit tests (`dotnet test`).
    6.  Publishes API and Web App artifacts separately.
* **Deployment Jobs (depend on successful build):**
    1.  `deploy_api`: Securely logs into Azure (OIDC), downloads API artifact, deploys to API App Service.
    2.  `deploy_web`: Securely logs into Azure (OIDC), downloads Web App artifact, deploys to Web App Service.

## Unit Testing

Unit tests are implemented for key backend components of the API project to ensure code quality and maintainability.

* **Frameworks Used:** xUnit and Moq.
* **Scope:** Includes tests for services like `BlobStorageService` and `BackgroundQueue`.
* This demonstrates a commitment to robust development practices.

## Getting Started / Local Setup

### Prerequisites

* .NET 8 SDK
* Git
* IDE (e.g., VS Code)
* (Optional) Azure CLI
* SQL Server instance (LocalDB, Express, Docker, or Azure SQL dev instance)
* (Optional) Database management tool (Azure Data Studio, SSMS)
* Azure Storage account
* Azure Custom Vision and AI Vision (formerly Computer Vision) services

### Cloning the Repository

```bash
git clone [https://github.com/jerhow/AI-medical-image-processor.git](https://github.com/jerhow/AI-medical-image-processor.git)
cd AI-medical-image-processor
```

### Configuration (User Secrets)

Initialize User Secrets for both API and Web App projects (`dotnet user-secrets init` in each project folder).

**1. API Project (`src/MedicalImageAI.Api`):**
```bash
dotnet user-secrets set "AzureSql:ConnectionString" "YOUR_LOCAL_SQL_SERVER_CONNECTION_STRING"
dotnet user-secrets set "BlobStorage:ConnectionString" "UseDevelopmentStorage=true" # For Azurite emulator, or your Azure Storage connection string
dotnet user-secrets set "BlobStorage:ContainerName" "YOUR_CONTAINER_NAME"
dotnet user-secrets set "CustomVision:PredictionKey" "YOUR_CV_PREDICTION_KEY"
dotnet user-secrets set "CustomVision:Endpoint" "YOUR_CV_ENDPOINT_URL"
dotnet user-secrets set "CustomVision:ProjectId" "YOUR_CV_CLASSIFICATION_PROJECT_ID"
dotnet user-secrets set "CustomVision:PublishedModelName" "YOUR_CV_CLASSIFICATION_PUBLISHED_MODEL_NAME"
dotnet user-secrets set "CustomVisionOD:ProjectId" "YOUR_CV_OBJECT_DETECTION_PROJECT_ID"
dotnet user-secrets set "CustomVisionOD:PublishedModelName" "YOUR_CV_OD_PUBLISHED_MODEL_NAME"
dotnet user-secrets set "CustomVisionOD:ConfidenceThreshold" "YOUR_CONFIDENCE_THRESHOLD" 
dotnet user-secrets set "CognitiveServicesVision:Endpoint" "YOUR_AZURE_AI_VISION_ENDPOINT"
dotnet user-secrets set "CognitiveServicesVision:Key" "YOUR_AZURE_AI_VISION_KEY"
dotnet user-secrets set "Security:ApiKey" "YOUR_STRONG_DEV_API_KEY"
dotnet user-secrets set "BackgroundQueue:Capacity" "YOUR_CAPACITY_VALUE" 
```

**2. Web App Project (`src/MedicalImageAI.Web`):**
```bash
dotnet user-secrets set "ApiSettings:BaseUrl" "https://localhost:7205" # Adjust if your local API runs on a different HTTPS port
dotnet user-secrets set "ApiClientSettings:ApiKey" "YOUR_STRONG_DEV_API_KEY" # Must match the API's Security:ApiKey
```
*Note: `ASPNETCORE_ENVIRONMENT` is typically `Development` via `launchSettings.json` for local runs.*

### Database Setup (Local)

1.  Ensure your local SQL Server instance is running.
2.  Navigate to `src/MedicalImageAI.Api`.
3.  Apply Migrations: `dotnet ef database update`

### Running Locally

1.  **API:** `cd src/MedicalImageAI.Api` then `dotnet run --launch-profile https` (Typically `https://localhost:7205`, Swagger at `/swagger`).
2.  **Web App:** `cd src/MedicalImageAI.Web` then `dotnet run` (Typically `https://localhost:7XXX`, check console. Upload page at `/Upload`).

### Running Unit Tests

From solution root or `tests/MedicalImageAI.Api.Tests`:
```bash
dotnet test
```

## Future Enhancements / Roadmap

* Further refinement of AI models (more data, additional pathology classifications, exploring different model architectures if needed)
* Support for additional medical imaging modalities (e.g., MRI, CT scans, dermatology images)
* More advanced filtering and querying options for the CSV report
* Enhanced UI for reviewing analysis results with more interactivity
* Implementation of user accounts and role-based access for the web application
* More comprehensive unit and potentially integration test coverage
* Deeper error handling and resilience patterns (e.g., retry mechanisms for background tasks)
* Refactor the UI toward a responsive design, suitable for use on a tablet or as an embeddable UI component

## License

This project is licensed under the [MIT License](https://github.com/jerhow/AI-medical-image-processor/blob/main/LICENSE)

Copyright (c) 2025 Jerry Howard
