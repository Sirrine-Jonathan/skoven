/**
 * Flowstate Companion App — entry point
 *
 * Usage:
 *   cp .env.example .env
 *   # fill in your API key and project path
 *   npm install
 *   npm run dev        # development (ts-node)
 *   npm run build && npm start   # production
 */

import dotenv from "dotenv";
import path from "path";

// Load .env from companion/ directory
dotenv.config({ path: path.resolve(process.cwd(), ".env") });

import { startServer } from "./server/index.js";

console.log("╔═══════════════════════════════════════════╗");
console.log("║        Flowstate Companion App            ║");
console.log("║  Voice-driven AI coding — Meta Quest      ║");
console.log("╚═══════════════════════════════════════════╝");
console.log();

startServer();
