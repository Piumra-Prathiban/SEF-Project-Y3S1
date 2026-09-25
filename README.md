# SE3090 AI Fashion Commerce & Retail Management Platform

SE3090 Year 3 Semester 1 group project: an integrated fashion e-commerce and retail management platform using **ASP.NET Core, PostgreSQL, React, Flutter, and Agentic AI**.

## Project Goal

Build one connected system where:

- **Flutter** → customer shopping application
- **React** → staff/admin management application
- **ASP.NET Core** → authoritative backend, business rules and security
- **PostgreSQL** → shared database
- **Agentic AI** → controlled multi-agent workflows

The clients must NOT directly access PostgreSQL or the Agentic AI service.

```text
Flutter ───────┐
               │
               ▼
        ASP.NET Core API
          │           │
          ▼           ▼
     PostgreSQL    Agentic AI
          │           │
          └─────┬─────┘
                ▼
             React
````

## Main Business Components

1. **Product & Inventory Management**

   * Products, categories, collections
   * Variants, sizes, colours
   * Inventory and stock updates

2. **Shopping & Customer Experience**

   * Search, filtering and browsing
   * Wishlist and cart
   * Customer profile
   * Recommendations

3. **Orders & Fulfilment**

   * Checkout and payment
   * Orders and order status
   * Cancellation and returns
   * Fulfilment

4. **Marketing & Business Intelligence**

   * Promotions and campaigns
   * Sales analytics
   * Inventory/customer analytics
   * Reports and demand insights

Each group member owns one business component, but every member contributes across backend, database, React, Flutter, testing, Git and Agentic AI.

## User Roles

### Customer

Uses Flutter for the shopping experience (products, cart, wishlist, profile and recommendations). The orders screens exist in the codebase but are not yet wired into the app shell.

### Staff / Inventory Manager

Uses React for products, inventory, orders, fulfilment and returns.

### Administrator

Uses React for users, permissions, promotions, reports, AI approvals and audit logs.

## Agentic AI

The project contains four distinct agents:

### 1. Planning / Coordinator Agent

Receives a user objective, creates a structured plan and delegates tasks.

### 2. Personal Stylist Agent

Uses customer preferences and real catalogue data to create fashion recommendations.

**Must never invent products, prices, variants or stock.**

### 3. Inventory & Promotion Agent

Checks stock, pricing, sales information and promotions and may propose alternatives/actions.

### 4. Validation & Business Rules Agent

Performs deterministic checks such as:

* Product exists
* Variant exists
* Stock available
* Size available
* Correct price
* Valid promotion
* Budget compliance
* Valid output structure

Agents must have distinct responsibilities, input/output contracts, controlled tools and visible participation.

## Required AI Workflow

```text
Customer objective
      ↓
Flutter
      ↓
ASP.NET Core
      ↓
Create AgentWorkflow
      ↓
Planning Agent
      ↓
Stylist Agent
      ↓
Inventory/Promotion Agent
      ↓
Validation Agent
      ↓
Deterministic validation
      ↓
Human approval if required
      ↓
React Admin
      ↓
Approve / Reject / Revise
      ↓
ASP.NET Core
      ↓
PostgreSQL
      ↓
Updated result/status
      ↓
Flutter
```

Agent workflows must support:

* Planning
* Delegation
* Allow-listed tools
* Structured inputs/outputs
* Persisted workflow state
* Deterministic validation
* Human approval
* Execution/audit history
* Error handling
* Safe failure

Do not store hidden model reasoning, passwords, tokens or secrets.

## Backend Rules

ASP.NET Core is the authoritative business layer.

Use:

* REST APIs
* DTOs
* Service/application layer
* Entity Framework Core
* PostgreSQL
* JWT authentication
* Role-based authorization
* Server-side validation
* Global error handling
* Swagger/OpenAPI
* Structured logging

Never trust client-provided:

* Prices
* Stock
* Discounts
* Permissions
* Important business decisions

Each student-owned component requires at least **4 meaningful API endpoints** and **1 business-specific operation beyond CRUD**.

## Database

Use PostgreSQL + EF Core migrations.

Main entities include:

```text
Users / Roles / UserRoles
Customers / Addresses
Categories / Collections
Products / ProductImages / ProductVariants
Sizes / Colours / Inventory
Wishlists / WishlistItems
Carts / CartItems
Orders / OrderItems / Payments
Returns / ReturnItems
Promotions / PromotionProducts
Reviews
AgentWorkflows
AgentWorkflowSteps
AgentToolExecutions
AgentValidationResults
AgentApprovals
AgentExecutionLogs
```

Use proper PKs, FKs, constraints, indexes, timestamps and transactions where required.

## React

React is the staff/admin application.

Must support appropriate:

* Routing
* Protected routes
* Role-based navigation
* API integration
* CRUD
* Search/filter/sort
* Pagination
* Validation
* Loading/error/empty states
* Dashboards
* AI workflow monitoring
* Approval/rejection/revision

## Flutter

Flutter is the customer application.

Must support appropriate:

* Registration/login/logout
* Secure token storage
* Product browsing
* Search/filtering
* Product details/variants
* Wishlist
* Cart
* Checkout/payment
* Orders/tracking
* Returns
* AI recommendations
* At least one meaningful device feature

Flutter communicates with the ASP.NET Core API only.

## Third-Party Integration

At least one meaningful third-party service is required.

Current planned option:

**Payment sandbox**

External credentials must remain on the backend.

Handle failures, invalid responses and timeouts safely.

## Testing

Required testing areas:

* Backend unit/service/API tests
* PostgreSQL integration tests
* React tests
* Flutter tests
* End-to-end testing
* Agentic AI evaluation
* Performance testing

Agent evaluation should include deterministic assertions, schema validation and golden cases. LLM-as-a-judge must not be the only evaluation method.

## Git & CI

Use:

* Git
* GitHub
* Feature branches
* Meaningful commits
* Pull requests
* Code reviews
* Issues/project board
* GitHub Actions

CI should restore, build and run backend tests on pushes and pull requests.

Do not create artificial Git history.

## AI Coding Agent Rules

Before changing code:

1. Inspect the existing repository.
2. Check `git status`.
3. Understand existing architecture.
4. Find related entities, APIs, components and tests.
5. Reuse existing patterns.
6. Make the smallest necessary change.
7. Add/update tests.
8. Run tests/build.
9. Check for security and API compatibility.

### Never:

* Rewrite unrelated working code
* Delete tests to make builds pass
* Bypass authorization
* Bypass validation
* Connect React/Flutter directly to PostgreSQL
* Let clients directly call Agentic AI
* Invent catalogue data
* Hard-code secrets
* Commit API keys/passwords/tokens
* Break API contracts without checking consumers
* Create fake test results or Git history

## Definition of Done

A feature should normally include:

```text
Database
→ Migration
→ Backend entity/service
→ DTO
→ API
→ Validation/Authorization
→ React
→ Flutter
→ Tests
→ Error handling
→ Documentation where required
```

For Agentic AI also require:

```text
Agent responsibility
→ Input/output contract
→ Controlled tools
→ Tool validation
→ Persisted state
→ Deterministic validation
→ Approval where required
→ Execution logging
→ Failure handling
→ Tests
```

1. Clone the repository.
2. Backend (ASP.NET Core)
   cd backend/SEF_Project.Api
   dotnet restore
   dotnet build

3. Frontend (React)
   cd frontend/SEF-Project
   npm install

4. Flutter
   cd mobile/sef_project_mobile/sef_project
   flutter pub get

5. PostgreSQL
   - Install PostgreSQL
   - Create your local database
   - Configure your local connection string
   - Do NOT commit passwords/API keys/secrets

6. EF Core
   - Check that the connection string is correct
   - Apply the existing migrations to your local database
   - Seed data if the project has seed data


## Local Database Setup

Member 2 technical documentation and contribution evidence:

- [Shopping and Customer Experience](docs/MEMBER2_TECHNICAL_DOCUMENTATION.md)

1. Make sure PostgreSQL is installed and running.
2. Create a database named `sef_project_db`.
3. Copy:

   `appsettings.Development.example.json`

   to:

   `appsettings.Development.json`

4. Update the PostgreSQL password in
   `appsettings.Development.json`.

5. Copy `.env.example` to `.env` (in `backend/SEF_Project.Api/`) and set the
   bootstrap Administrator details:

   `SeedAdmin__Email` and `SeedAdmin__Password`

6. Run:

   `dotnet ef database update`

7. Start the API:

   `dotnet run`

The `.env` file is gitignored. On startup the API creates that Administrator
account when the email does not exist yet (existing accounts are never modified),
so no manual SQL is needed — sign in with those credentials to reach the
staff/admin screens. Public registration always creates Customers.


## Important

The **source code and tests are the implementation truth**.

This README describes the intended project architecture and requirements. Always inspect the current code before making assumptions.

The final system must demonstrate one complete workflow connecting:

**Flutter → ASP.NET Core → PostgreSQL → Agentic AI → React approval → shared updated state → Flutter**

AI tools may be used during development under the SE3090 rules, but every team member must understand, test and be able to modify their submitted work. External AI tools cannot be used during the final demonstration/viva.

