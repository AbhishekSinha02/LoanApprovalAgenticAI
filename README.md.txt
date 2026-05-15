  Local dev

  Set USE_LOCAL_INFRA=true in appsettings.Development.json (already done) and run docker compose -f docker-compose.local.yml up to start Ollama. Then dotnet run --project src/Api and open
  https://localhost:5001/swagger.
  
   Also fix the docker-compose.local.yml context path — the build.context needs to point to the repo root, not src/Api:

  Read 1 file (ctrl+o to expand)

● The context: . and dockerfile: src/Api/Dockerfile is correct (context is repo root, Dockerfile path is relative to
  it). The compose file is fine. Now you need to pull the model once Ollama is running:

  Step 1 — Start infra only (run this now):
  docker compose -f docker-compose.local.yml up ollama azurite -d

  Step 2 — Pull the model (Ollama needs a model before it can answer requests):
  docker exec -it loan-approval-local-ollama-1 ollama pull llama3.1

  Step 3 — Run the API (separate terminal):
  dotnet run --project src/Api

  The model pull takes a few minutes (~4 GB). Once it's done, http://localhost:5091/swagger will work and you can submit
   a loan application.
