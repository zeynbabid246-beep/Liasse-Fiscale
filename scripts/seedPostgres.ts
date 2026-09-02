import fs from 'fs';
import path from 'path';
import pg from 'pg';

const { Pool } = pg;

async function runSeed() {
  const connectionString = process.env.DATABASE_URL || 'postgresql://postgres:postgres@localhost:5432/liasse_fiscale_db';
  console.log('--- Peuplement de la base PostgreSQL (Liasse Fiscale) ---');
  console.log('Connexion cible :', connectionString.replace(/:[^:@]+@/, ':****@'));

  const pool = new Pool({ connectionString });

  try {
    const client = await pool.connect();
    console.log('✅ Connexion établie avec succès à PostgreSQL.');

    const schemaPath = path.join(process.cwd(), 'src', 'db', 'schema.sql');
    if (!fs.existsSync(schemaPath)) {
      throw new Error(`Fichier de schéma introuvable : ${schemaPath}`);
    }

    const sqlContent = fs.readFileSync(schemaPath, 'utf8');
    await client.query(sqlContent);
    console.log('✅ Tables créées et données d\'exemples insérées avec succès dans pgAdmin !');

    // Vérification des comptes insérés
    const usersRes = await client.query('SELECT COUNT(*) FROM users');
    const depositsRes = await client.query('SELECT COUNT(*) FROM deposits');
    const filesRes = await client.query('SELECT COUNT(*) FROM deposit_files');
    const detailsRes = await client.query('SELECT COUNT(*) FROM declaration_details');
    const logsRes = await client.query('SELECT COUNT(*) FROM audit_logs');

    console.log('\n📊 Résumé des tables dans la base :');
    console.log(`  - users : ${usersRes.rows[0].count} enregistrements`);
    console.log(`  - deposits : ${depositsRes.rows[0].count} enregistrements`);
    console.log(`  - deposit_files : ${filesRes.rows[0].count} enregistrements`);
    console.log(`  - declaration_details : ${detailsRes.rows[0].count} enregistrements`);
    console.log(`  - audit_logs : ${logsRes.rows[0].count} enregistrements`);

    client.release();
    await pool.end();
  } catch (err: any) {
    console.error('❌ Erreur lors du peuplement de la base :', err.message);
    process.exit(1);
  }
}

runSeed();
