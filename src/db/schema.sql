-- =============================================================================
-- SCHÉMA DE BASE DE DONNÉES : LIASSE FISCALE (POSTGRESQL / PGADMIN)
-- =============================================================================

-- 1. Table des utilisateurs & télé-déclarants
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    matricule_fiscal VARCHAR(20) UNIQUE NOT NULL,
    raison_sociale VARCHAR(255) NOT NULL,
    role VARCHAR(50) DEFAULT 'DECLARANT', -- 'DECLARANT' ou 'ADMIN'
    annee_exercice INTEGER NOT NULL DEFAULT 2026,
    code_systeme VARCHAR(50) NOT NULL DEFAULT 'SYSTEME_NORMAL',
    modele VARCHAR(50) DEFAULT 'MODELE_NORMAL',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. Table des dossiers de dépôts de Liasse Fiscale
CREATE TABLE IF NOT EXISTS deposits (
    id VARCHAR(50) PRIMARY KEY,
    matricule_fiscal VARCHAR(20) NOT NULL,
    raison_sociale VARCHAR(255) NOT NULL,
    annee_exercice INTEGER NOT NULL,
    code_systeme VARCHAR(50) NOT NULL,
    modele VARCHAR(50) NOT NULL,
    statut VARCHAR(50) NOT NULL DEFAULT 'BROUILLON', -- 'BROUILLON', 'VALIDE', 'REJETE', 'DEPOSE'
    date_depot TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    quittance_numero VARCHAR(50),
    quittance_path VARCHAR(500),
    erreurs_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Table des fichiers déposés (XML formulaires et PDF rapports)
CREATE TABLE IF NOT EXISTS deposit_files (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50) NOT NULL REFERENCES deposits(id) ON DELETE CASCADE,
    code_document VARCHAR(20) NOT NULL,
    nom_fichier_original VARCHAR(255) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_size_bytes BIGINT,
    mime_type VARCHAR(100),
    statut_validation VARCHAR(50) DEFAULT 'EN_ATTENTE', -- 'VALIDE', 'ERREUR', 'EN_ATTENTE'
    rapport_validation JSONB,
    uploaded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. Table des soldes et détails déclarés (rubriques comptables)
CREATE TABLE IF NOT EXISTS declaration_details (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50) NOT NULL REFERENCES deposits(id) ON DELETE CASCADE,
    code_document VARCHAR(20) NOT NULL,
    code_rubrique VARCHAR(50) NOT NULL,
    valeur_declaree NUMERIC(18, 3) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_deposit_doc_rubrique UNIQUE (deposit_id, code_document, code_rubrique)
);

-- 5. Table d'audit et traçabilité
CREATE TABLE IF NOT EXISTS audit_logs (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50),
    matricule_fiscal VARCHAR(20) NOT NULL,
    action VARCHAR(100) NOT NULL,
    details TEXT,
    ip_address VARCHAR(45),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Index pour optimiser les performances de recherche
CREATE INDEX IF NOT EXISTS idx_deposits_matricule ON deposits(matricule_fiscal);
CREATE INDEX IF NOT EXISTS idx_deposits_exercice ON deposits(annee_exercice);
CREATE INDEX IF NOT EXISTS idx_files_deposit ON deposit_files(deposit_id);
CREATE INDEX IF NOT EXISTS idx_audit_matricule ON audit_logs(matricule_fiscal);

-- Données initiales
INSERT INTO users (matricule_fiscal, raison_sociale, role, annee_exercice, code_systeme, modele)
VALUES 
('0000121J', 'SOCIETE EXEMPLE INDUSTRIE SARL', 'DECLARANT', 2026, 'SYSTEME_NORMAL', 'MODELE_NORMAL'),
('1234567A', 'COMMERCE INTERNATIONAL TUNISIE', 'DECLARANT', 2024, 'SYSTEME_NORMAL', 'MODELE_NORMAL'),
('ADMIN001', 'DIRECTION GÉNÉRALE DES IMPÔTS', 'ADMIN', 2026, 'SYSTEME_NORMAL', 'MODELE_NORMAL')
ON CONFLICT (matricule_fiscal) DO NOTHING;
