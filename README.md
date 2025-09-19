# Quran Video Generator

Create beautiful **9:16 vertical Qur’an videos** with recitation and synced ayah overlays—ready for Shorts, Reels, and Stories.

<p align="left">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white">
  <img alt="ASP.NET Core MVC" src="https://img.shields.io/badge/ASP.NET%20Core-MVC-5C2D91">
  <img alt="Bootstrap" src="https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap&logoColor=white">
  <img alt="FFmpeg" src="https://img.shields.io/badge/FFmpeg-required-007808?logo=ffmpeg&logoColor=white">
  <img alt="License" src="https://img.shields.io/badge/License-MIT-green">
</p>

---

## Table of Contents

- [Features](#features)
- [Architecture & Flow](#architecture--flow)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Quick Start (Development)](#quick-start-development)
- [Configuration](#configuration)
- [FFmpeg Notes](#ffmpeg-notes)
- [Pexels Integration](#pexels-integration)
- [Fonts & Arabic Typography](#fonts--arabic-typography)
- [Generations Page (Client-Side)](#generations-page-client-side)
- [Contact Form & CAPTCHA](#contact-form--captcha)
- [Build & Publish](#build--publish)
- [IIS Deployment Guide](#iis-deployment-guide)
- [URL Rewrite & HTTPS](#url-rewrite--https)
- [Maintenance & Cleanup](#maintenance--cleanup)
- [Troubleshooting](#troubleshooting)
- [Project Structure](#project-structure)
- [Roadmap](#roadmap)
- [License](#license)

---

## Features

- 9:16 **vertical** MP4 output (yuv420p) suitable for Shorts/Reels/Stories.
- **Pexels** background videos (portrait nature/islamic queries; configurable).
- **Recitation audio** concatenated by ayah & **centered Arabic overlays**.
- In-app **preview**, **download**, and “Re-Generate” UX.
- **Generations** page showing a user’s recent outputs (24h, LocalStorage).
- **Contact** form with **Cloudflare Turnstile** (invisible CAPTCHA).
- Simple JSON content for **reciters**, **surah list**, **video styles**.
- Production-ready **IIS** deployment (Windows), with rewrite & HTTPS notes.

---

## Architecture & Flow

1. User fills form (reciter, surah, ayahs, style).
2. Backend validates request → fetches recitation audio & Pexels background.
3. Captions are rendered as **PNG overlays**.
4. **FFmpeg** composes final video (background + audio + overlays).
5. Video is saved under `/wwwroot/generated/` with unique name.
6. User can **preview, download**, or check history under **Generations**.

---

## Tech Stack

- **Backend**: ASP.NET Core 8 (MVC, C#)
- **Frontend**: Bootstrap 5, Vanilla JS
- **Media Processing**: FFmpeg
- **Storage**: Local file system (`wwwroot/generated`)
- **Backgrounds**: Pexels API
- **Fonts**: Amiri/Quranic Arabic
- **Deployment**: IIS / Windows Server

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [FFmpeg](https://ffmpeg.org/download.html) installed & in PATH
- Pexels API key (optional but recommended)
- IIS (for deployment) or Kestrel for dev

---

## Quick Start (Development)

```bash
# clone the repo
git clone https://github.com/your-repo/quran-video-generator.git
cd quran-video-generator

# restore & run
dotnet restore
dotnet watch run
```

Access at: `https://localhost:5001/`

---

## Configuration

App settings (`appsettings.json`):

```json
{
  "Video": {
    "DemoMode": true,
    "OutputRoot": "wwwroot/generated"
  },
  "Pexels": {
    "ApiKey": "YOUR_PEXELS_KEY",
    "BaseUrl": "https://api.pexels.com/videos/search?query=mosque&orientation=portrait"
  },
  "Captcha": {
    "SiteKey": "your-turnstile-site-key",
    "SecretKey": "your-turnstile-secret-key"
  }
}
```

---

## FFmpeg Notes

- Ensure FFmpeg is in PATH:
  ```bash
  ffmpeg -version
  ```
- Video composition:
  - Rescale to **1080x1920**
  - Add contrast/brightness/saturation
  - Apply fade in/out
  - Overlay ayah PNGs centered
- Output: `MP4 (H.264 + AAC)` for wide compatibility.

---

## Pexels Integration

- Requires **API key**: [https://www.pexels.com/api/](https://www.pexels.com/api/)
- Videos are fetched dynamically per style query.
- Demo mode (`DemoMode=true`) generates plain black background.

---

## Fonts & Arabic Typography

- Default: [Amiri](https://www.amirifont.org/) for Arabic text.
- Captions are generated with **right-to-left alignment**.
- You can add custom Quranic fonts under `/wwwroot/fonts/`.

---

## Generations Page (Client-Side)

- Stores results in `localStorage` under key: `qvg.generations`.
- Each record: reciter, surah, ayahs, style, video URL, date.
- Expired (24h+) entries auto-filtered.
- Displayed as **Bootstrap cards** with **Download** button.

---

## Contact Form & CAPTCHA

- Fields: Full Name, Email, Subject, Message.
- Stored as JSON file: `/wwwroot/feedback/dd-MM-yyyy_feedback.json`.
- CAPTCHA: [Cloudflare Turnstile](https://developers.cloudflare.com/turnstile/) (invisible, user-friendly).
- Validates both client-side and server-side.

---

## Build & Publish

```bash
# clean previous builds
dotnet clean

# publish release
dotnet publish --configuration Release
```

Output in: `/bin/Release/net8.0/publish/`

---

## IIS Deployment Guide

1. Install **IIS + ASP.NET Core Hosting Bundle**.
2. Deploy published files to `C:\inetpub\quranvideogen`.
3. Configure App Pool:
   - .NET CLR: No Managed Code
   - Enable 32/64-bit: Default
4. Add **MIME types** if needed:
   - `.mp4 → video/mp4`
   - `.json → application/json`
5. Set permissions for `wwwroot/generated/`.

---

## URL Rewrite & HTTPS

- Install IIS **URL Rewrite** module.
- Redirect HTTP → HTTPS:
  ```xml
  <rule name="Redirect to HTTPS" stopProcessing="true">
    <match url="(.*)" />
    <conditions>
      <add input="{HTTPS}" pattern="off" ignoreCase="true" />
    </conditions>
    <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
  </rule>
  ```

---

## Maintenance & Cleanup

- Generated videos stored under `/wwwroot/generated/`.
- Auto-clean (optional CRON/Task Scheduler): delete files > 24h old.
- Feedback JSON files can be archived monthly.

---

## Troubleshooting

- **FFmpeg not found** → ensure `ffmpeg.exe` is in PATH.
- **Fonts missing** → check `/wwwroot/fonts/`.
- **Pexels API error** → verify API key & quota.
- **Feedback not saving** → ensure write permissions for `wwwroot/feedback/`.

---

## Project Structure

```
QuranVideoGeneration.Web/
├── Controllers/
│   ├── VideoController.cs
│   ├── FeedbackController.cs
│   └── HomeController.cs
├── Services/
│   ├── MediaService.cs
│   ├── CaptionService.cs
│   └── RecitationService.cs
├── Views/
│   ├── Home/Index.cshtml
│   ├── Home/Generations.cshtml
│   └── Shared/_Layout.cshtml
├── wwwroot/
│   ├── css/
│   ├── js/
│   ├── fonts/
│   ├── generated/
│   └── feedback/
└── appsettings.json
```

---

## Roadmap

- [ ] Add **user accounts** & personal video history.
- [ ] Enable **multiple background sources**.
- [ ] Add **share-to-social** (YouTube Shorts, Instagram Reels).
- [ ] Support **custom watermark/logo overlays**.
- [ ] Expand reciter & translation options.

---

## License

MIT License © 2025 — Open for personal & educational use.
