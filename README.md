# Portail de Dépôt de Liasse Fiscale — Cas Général

## Prérequis
- .NET SDK 8.0
- PostgreSQL 16 (ou Docker)

## Démarrage local

```bash
cd LiasseFiscale.Api
dotnet restore
dotnet ef database update   # nécessite dotnet-ef : dotnet tool install --global dotnet-ef
dotnet run
```

Swagger disponible sur `https://localhost:5001/swagger` en mode Development.

## Démarrage avec Docker

```bash
docker compose up --build
```

## Régénérer les schémas structurels et les règles métier

Si la DGI publie une nouvelle version des XSD, remplacer les fichiers dans
`LiasseFiscale.Api/SchemaAssets/original/`, puis :

```bash
cd Tools/SchemaPreprocessor
dotnet run -- ../../LiasseFiscale.Api/SchemaAssets/original ../../LiasseFiscale.Api/SchemaAssets/structural ../../LiasseFiscale.Api/SchemaAssets/rules
```

Puis committer les fichiers régénérés (`structural/`, `rules/`) — voir la checklist
pour l'explication de pourquoi ce n'est pas régénéré automatiquement en prod.

## État du moteur de règles métier

- F6001, F6002, F6003, F6004 : toutes les formules d'agrégation sont vérifiées automatiquement.
- F6005 : seules 14 des 380 assertions (les sommes simples) sont vérifiées ; les 366 règles
  conditionnelles restantes (logique PP/SP+SC) sont listées par
  `IAssertRuleEngine.ObtenirReglesComplexesNonImplementees` mais pas encore interprétées —
  voir la checklist, Jour 2.
