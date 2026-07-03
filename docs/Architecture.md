## Overview

FactoryPulse is a cloud-native manufacturing analytics platform designed to help production managers monitor machines, production orders, downtime and factory KPIs.

The project is being built to demonstrate modern .NET backend development using ASP.NET Core, Entity Framework Core, SQL Server, Docker and Microsoft Azure.

## Users

- Production Manager
- Factory Supervisor
- Plant Manager

## Core Features

- Monitor machines
- Track production orders
- Record downtime
- View production KPIs
- Secure API authentication

## High-Level Architecture

Angular Frontend (Later)
↓
ASP.NET Core REST API
↓
Service Layer (Business Logic)
↓
Repository Layer (Data Access)
↓
Entity Framework Core
↓
SQL Server / Azure SQL


Controller
    ↓
Service
    ↓
Repository
    ↓
DbContext

## Technology Stack
.NET 10
ASP.NET Core Web API
Entity Framework Core
SQL Server
Azure SQL
Docker
Azure App Service
GitHub Actions
Angular (Later)