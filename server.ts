import express, { Request, Response } from 'express';
import cors from 'cors';
import path from 'path';
import http from 'http';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 3000;
const ASPNETCORE_API_URL = process.env.ASPNETCORE_URL || 'http://127.0.0.1:5000';

app.use(cors());

// Distribution des fichiers statiques du frontend
app.use(express.static(path.join(__dirname, 'public')));

// Acheminement unique et direct vers le backend ASP.NET Core
app.use('/api', (req: Request, res: Response) => {
  const targetUrl = new URL(req.originalUrl, ASPNETCORE_API_URL);

  const proxyReq = http.request(
    targetUrl.toString(),
    {
      method: req.method,
      headers: {
        ...req.headers,
        host: targetUrl.host,
        'x-forwarded-for': req.ip,
        'x-forwarded-proto': req.protocol,
        'x-forwarded-host': req.headers.host
      }
    },
    (proxyRes) => {
      res.writeHead(proxyRes.statusCode || 200, proxyRes.headers);
      proxyRes.pipe(res);
    }
  );

  proxyReq.on('error', (err) => {
    console.error(`[Proxy Error] Échec de transmission vers l'API ASP.NET Core (${ASPNETCORE_API_URL}) :`, err.message);
    if (!res.headersSent) {
      res.status(502).json({
        error: "Passerelle API ASP.NET Core indisponible",
        message: "L'unique backend ASP.NET Core (LiasseFiscale.Api) traite toutes les validations et requêtes.",
        target: ASPNETCORE_API_URL
      });
    }
  });

  // Redirection du flux (y compris upload de fichiers multipart XML/PDF)
  req.pipe(proxyReq);
});

// Route de repli pour SPA
app.get('*', (_req: Request, res: Response) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Portail Liasse Fiscale Frontend + Reverse Proxy démarré sur http://0.0.0.0:${PORT}`);
  console.log(`Backend unique ASP.NET Core configuré sur : ${ASPNETCORE_API_URL}`);
});
