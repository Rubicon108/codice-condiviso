# Codice Condiviso

Editor collaborativo per sketch Arduino. GitHub Pages ospita l'interfaccia; Supabase conserva i workspace; l'app Windows usa Arduino CLI sulla macchina collegata alla scheda.

## Stato

Repository iniziale. La sincronizzazione richiede un progetto Supabase configurato; l'app desktop viene compilata da GitHub Actions.

## Backend Supabase

1. Crea un progetto Supabase.
2. Esegui `backend/schema.sql` nell'SQL Editor.
3. Copia l'URL progetto e la chiave anon pubblica nel file `config.js` nella radice (compila `supabaseUrl` e `supabaseAnonKey`).
4. Pubblica il sito con GitHub Actions. In Settings → Pages scegli GitHub Actions.

Il PIN separa i workspace, ma non è autenticazione forte. Chi lo conosce può leggere e modificare quel workspace. Non inserire segreti o credenziali negli sketch condivisi.

## App Windows

Il workflow `Build Windows` pubblica `CodiceCondiviso-Setup.exe` come artifact. L'app usa Arduino CLI locale: collega la scheda al PC, seleziona la porta COM e installa il core della scheda con Arduino CLI prima del primo build. La seriale è impostata a 9600 baud.

## Dominio

Con GitHub Pages attivo: https://rubicon108.github.io/codice-condiviso/
