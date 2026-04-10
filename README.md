# Objednávací systém v menze (Půlsemestrální odevzdání)

Tento projekt je backendová část objednávacího systému pro univerzitní menzu. Je postaven na platformě **.NET 10** s využitím **.NET Aspire** pro orchestraci a **Entity Framework Core** pro práci s databází PostgreSQL.

## 🏛️ Architektonická rozhodnutí

Při návrhu backendu jsme se řídili požadavky zadání a moderními .NET standardy:

1. **Struktura projektů:** Řešení je rozděleno do logických vrstev (`AppHost`, `Db`, `DbManager`, `Contracts`, `WebApi` a `WebApi.Tests`), aby byla oddělena databázová logika od API a orchestrace.
2. **Minimal Web API:** Místo tradičních Controllerů jsme využili Minimal APIs s návratovými typy `TypedResults`, což snižuje overhead a zpřehledňuje kód.
3. **Data Transfer Objects (DTO):** API v žádném endpointu nevrací databázové entity přímo. Veškerá komunikace probíhá přes neměnné `record` typy definované izolovaně v projektu `UTB.Minute.Contracts`.
4. **Řešení souběžnosti (Concurrency):** Pro zabránění chybám při objednávání posledních porcí (např. dva studenti kliknou ve stejnou chvíli) využíváme skrytý sloupec `xmin` v PostgreSQL jako *Concurrency Token*. Pokud dojde ke konfliktu, API zachytí `DbUpdateConcurrencyException` a vrátí HTTP 409 Conflict.
5. **Soft Delete:** Položky jídel (`Food`) se z databáze nemažou příkazem DELETE, ale pouze se deaktivují pomocí příznaku `IsActive = false`, aby zůstala zachována integrita historických objednávek.

## 🚧 Problémy při řešení a jejich překonání

Během vývoje půlsemestrální části jsme narazili na několik výzev:
* **Verzování Aspire knihoven:** Při integraci automatizovaných testů (`Aspire.Hosting.Testing`) došlo k neshodě verzí s lokálním AppHostem. Problém jsme vyřešili sjednocením všech Aspire balíčků napříč projekty na verzi `13.2.2`.
* **SSL Certifikáty v testech:** Integrační testy padaly na neplatném SSL certifikátu při HTTPS komunikaci mezi testovacím klientem a lokálním API. Vyřešeno nastavením důvěryhodnosti lokálních vývojářských certifikátů (`dotnet dev-certs https --trust`).

## 🚀 Jak projekt spustit

Projekt je navržen tak, aby šel spustit jednoduše pomocí .NET Aspire (vyžaduje běžící Docker Desktop):

1. Otevřete terminál v kořenové složce projektu.
2. Přejděte do složky orchestrátoru: `cd UTB.Minute.AppHost`
3. Spusťte projekt: `dotnet run`
4. Aspire Dashboard se otevře v prohlížeči, odkud uvidíte běžící databázi i Web API.
5. Alternativně lze spustit testy ze složky řešení příkazem: `dotnet test`

## 👥 Rozdělení práce v týmu

Na projektu pracovali všichni členové týmu. Poměr odvedené práce je **1 : 1 : 1**.
