# Omny.Cms.Ui Features

This UI project is organized around features using a vertical slice style. Each top level folder under `Features` represents a functional area of the application. Subfolders contain pages, components, services and models related to that feature.

## Feature Overview

- **Authentication** – logic for API redirects and CSRF token management.
- **Editor** – the content editor UI, field plugins and supporting services.
- **Images** – image selection, cropping and storage helpers.
- **Layout** – shared layout and navigation components used across pages.
- **Repositories** – repository management, remote file access and build watcher services.
  - `Files/GitHub` contains the GitHub specific file service and client provider.

## Folder Structure

```
Features/
  Authentication/
  Editor/
    Components/
    Core/
    Pages/
  Images/
    Components/
    Pages/
    Services/
  Layout/
  Repositories/
    Components/
    Files/
      GitHub/
    Models/
    Services/
```

Each folder may include further subfeatures as needed.
