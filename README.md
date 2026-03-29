# CodeNames

Two teams race to uncover their secret words, guided only by cryptic one-word clues from their Spymaster. Every round, your team votes together on a guess, but the clock is always running and the other team isn't waiting around. Pick wrong and you might hand them an advantage. Pick the Assassin and it's instant game over. Simple rules...

## Team
- Trevor Naick (Team Lead)
- Aaliyah Noorbhai
- Maselo Selepe
- Massamba Maphalala
- Michael Todd

Tech stack: Java 26 / Spring Boot · .NET 10 / C# · PostgreSQL · AWS (af-south-1)

## Local Development

### Prerequisites

- Java 26
- Maven 3.9+
- .NET 10 SDK

### Server

**1. Create `server/.env`** from the example:

```bash
cp server/.env.example server/.env
```

Then fill in the values — generate a JWT secret with:

```bash
openssl rand -base64 32
```

**2. Run:**

```bash
cd server
mvn spring-boot:run
```

The server starts on `http://localhost:8080`.

### CLI

**1. Create `cli/Codenames.Cli/.env`** from the example:

```bash
cp cli/Codenames.Cli/.env.example cli/Codenames.Cli/.env
```

Then fill in the Google OAuth client secret (get it from the Google Cloud Console).

**2. Run:**

```bash
cd cli/Codenames.Cli
dotnet run
```

Select **Login** — a browser window opens for Google sign-in. After authenticating you'll be returned to the main menu.

### Infrastructure (local Terraform only)

If you need to run Terraform locally:

```bash
cp terraform/terraform.tfvars.example terraform/terraform.tfvars
# fill in real values, then:
cd terraform
terraform init
terraform apply
```

For the runner:

```bash
cp terraform/runner/terraform.tfvars.example terraform/runner/terraform.tfvars
# fill in your GitLab registration token, then:
cd terraform/runner
terraform init
terraform apply
```
