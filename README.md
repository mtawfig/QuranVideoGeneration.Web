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

