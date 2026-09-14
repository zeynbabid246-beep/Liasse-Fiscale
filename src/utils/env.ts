import fs from 'fs';
import path from 'path';

/**
 * Charge les variables définies dans un fichier .env dans process.env
 * sans écraser les variables d'environnement système déjà définies.
 */
export function loadEnv(customPath?: string): void {
  const envPath = customPath || path.resolve(process.cwd(), '.env');

  // Si l'environnement Node supporte la méthode native process.loadEnvFile (Node >= 20.6.0)
  if (typeof (process as any).loadEnvFile === 'function') {
    try {
      if (fs.existsSync(envPath)) {
        (process as any).loadEnvFile(envPath);
        return;
      }
    } catch {
      // En cas d'erreur de la méthode native, on continue vers le parseur de secours
    }
  }

  // Parseur de secours pour .env
  try {
    if (!fs.existsSync(envPath)) return;
    const content = fs.readFileSync(envPath, 'utf8');
    const lines = content.split(/\r?\n/);
    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line || line.startsWith('#')) continue;
      const eqIdx = line.indexOf('=');
      if (eqIdx === -1) continue;
      const key = line.slice(0, eqIdx).trim();
      let val = line.slice(eqIdx + 1).trim();

      // Gestion des guillemets doubles ou simples
      if (
        (val.startsWith('"') && val.endsWith('"')) ||
        (val.startsWith("'") && val.endsWith("'"))
      ) {
        val = val.slice(1, -1);
      }

      if (key && !(key in process.env)) {
        process.env[key] = val;
      }
    }
  } catch (err) {
    console.warn('⚠️ [ENV] Impossible de charger le fichier .env :', err);
  }
}

// Auto-chargement à l'import
loadEnv();

