import pg from 'pg';
import fs from 'fs';
import path from 'path';
const { Pool } = pg;
let pool = null;
let isConnected = false;
let initPromise = null;
/**
 * Récupère ou initialise le pool de connexions PostgreSQL de manière sécurisée et paresseuse (lazy).
 */
export function getPostgresPool() {
    const connectionString = process.env.DATABASE_URL;
    if (!connectionString) {
        return null;
    }
    if (!pool) {
        pool = new Pool({
            connectionString,
            max: 10,
            idleTimeoutMillis: 30000,
            connectionTimeoutMillis: 5000,
            ssl: connectionString.includes('sslmode=require') || connectionString.includes('.rds.') || connectionString.includes('supabase')
                ? { rejectUnauthorized: false }
                : false
        });
        pool.on('error', (err) => {
            console.warn('⚠️ [PostgreSQL] Erreur inattendue sur le pool de connexions:', err.message);
            isConnected = false;
        });
    }
    return pool;
}
/**
 * Initialise les tables et schémas PostgreSQL si la base est configurée
 */
export async function initPostgresDatabase() {
    if (initPromise) {
        return initPromise;
    }
    initPromise = (async () => {
        const currentPool = getPostgresPool();
        if (!currentPool) {
            console.log('ℹ️ [PostgreSQL] Aucune variable DATABASE_URL détectée. Fonctionnement en mode mémoire / fichiers locaux.');
            return false;
        }
        try {
            const client = await currentPool.connect();
            console.log('✅ [PostgreSQL] Connecté avec succès à la base de données.');
            isConnected = true;
            // Lecture du schéma SQL
            const schemaPath = path.join(process.cwd(), 'src', 'db', 'schema.sql');
            if (fs.existsSync(schemaPath)) {
                const schemaSql = fs.readFileSync(schemaPath, 'utf8');
                await client.query(schemaSql);
                console.log('✅ [PostgreSQL] Schéma et tables vérifiés/créés avec succès.');
            }
            client.release();
            return true;
        }
        catch (err) {
            console.warn(`⚠️ [PostgreSQL] Connexion impossible (${err.message}). L'application continue avec le stockage local.`);
            isConnected = false;
            return false;
        }
    })();
    return initPromise;
}
/**
 * Exécute une requête SQL sécurisée si connecté à PostgreSQL, sinon renvoie null
 */
export async function queryDb(sql, params = []) {
    const currentPool = getPostgresPool();
    if (!currentPool || !isConnected) {
        return null;
    }
    try {
        return await currentPool.query(sql, params);
    }
    catch (err) {
        console.warn(`⚠️ [PostgreSQL] Erreur lors de l'exécution SQL (${err.message}):`, sql.substring(0, 100));
        return null;
    }
}
/**
 * Enregistre un log d'audit dans PostgreSQL
 */
export async function logAuditDb(matricule, action, details, depositId, ip) {
    try {
        await queryDb(`INSERT INTO audit_logs (matricule_fiscal, action, details, deposit_id, ip_address)
       VALUES ($1, $2, $3, $4, $5)`, [matricule, action, details || null, depositId || null, ip || null]);
    }
    catch (err) {
        // Audit non-bloquant
    }
}
/**
 * Sauvegarde ou met à jour un dépôt dans PostgreSQL
 */
export async function saveDepositDb(deposit) {
    try {
        await queryDb(`INSERT INTO deposits (id, matricule_fiscal, raison_sociale, annee_exercice, code_systeme, modele, statut, quittance_numero, quittance_path, erreurs_count, updated_at)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, CURRENT_TIMESTAMP)
       ON CONFLICT (id) DO UPDATE SET
         statut = EXCLUDED.statut,
         quittance_numero = EXCLUDED.quittance_numero,
         quittance_path = EXCLUDED.quittance_path,
         erreurs_count = EXCLUDED.erreurs_count,
         updated_at = CURRENT_TIMESTAMP`, [
            deposit.id,
            deposit.matriculeFiscal,
            deposit.raisonSociale,
            deposit.anneeExercice,
            deposit.codeSysteme,
            deposit.modele,
            deposit.statut,
            deposit.quittanceNumero || null,
            deposit.quittancePath || null,
            deposit.erreursCount || 0
        ]);
    }
    catch (err) {
        console.warn('⚠️ [PostgreSQL] Impossible de persister le dépôt:', err.message);
    }
}
/**
 * Sauvegarde un fichier et son rapport de validation dans PostgreSQL
 */
export async function saveDepositFileDb(fileData) {
    try {
        await queryDb(`INSERT INTO deposit_files (deposit_id, code_document, nom_fichier_original, file_path, file_size_bytes, mime_type, statut_validation, rapport_validation)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8)`, [
            fileData.depositId,
            fileData.codeDocument,
            fileData.nomFichierOriginal,
            fileData.filePath,
            fileData.fileSizeBytes,
            fileData.mimeType,
            fileData.statutValidation,
            JSON.stringify(fileData.rapportValidation)
        ]);
    }
    catch (err) {
        console.warn('⚠️ [PostgreSQL] Impossible de persister le fichier de dépôt:', err.message);
    }
}
/**
 * Sauvegarde les rubriques déclarées (chiffres comptables) dans PostgreSQL
 */
export async function saveDeclarationDetailsDb(depositId, codeDocument, details) {
    if (!details || Object.keys(details).length === 0)
        return;
    try {
        for (const [rubrique, valeur] of Object.entries(details)) {
            if (typeof valeur === 'number' && !isNaN(valeur)) {
                await queryDb(`INSERT INTO declaration_details (deposit_id, code_document, code_rubrique, valeur_declaree)
           VALUES ($1, $2, $3, $4)
           ON CONFLICT (deposit_id, code_document, code_rubrique) DO UPDATE SET
             valeur_declaree = EXCLUDED.valeur_declaree`, [depositId, codeDocument, rubrique, valeur]);
            }
        }
    }
    catch (err) {
        console.warn('⚠️ [PostgreSQL] Impossible de persister les détails comptables:', err.message);
    }
}
