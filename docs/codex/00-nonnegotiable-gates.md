# Non-negotiable gates (every change)
From src/backend:
- dotnet restore .\Intentify.sln
- dotnet build -c Debug .\Intentify.sln
- dotnet test  -c Debug .\Intentify.sln

From src/frontend/web:
- npm ci
- npm run build

Rule: If any gate fails, STOP and fix immediately. Do not proceed to the next stage.
