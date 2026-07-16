// Petits helpers JS pour l'interop Blazor.
window.mariage = {
    isOnline: () => navigator.onLine,

    copyText: (text) => navigator.clipboard?.writeText(text) ?? Promise.resolve(),

    // Web Share API (native Android) avec fallback WhatsApp
    shareText: async (text) => {
        if (navigator.share) {
            await navigator.share({ text });
        } else {
            window.open('https://wa.me/?text=' + encodeURIComponent(text), '_blank');
        }
    },

    // Ouvre tous les jours, imprime, puis restaure l'état
    printPlan: () => {
        const details = document.querySelectorAll('details.day-card');
        const wasOpen = Array.from(details).map(d => d.open);
        details.forEach(d => { d.open = true; });
        window.addEventListener('afterprint', () => {
            details.forEach((d, i) => { d.open = wasOpen[i]; });
        }, { once: true });
        window.print();
    },

    // --- Partage de configuration chiffrée (URL / QR code) ---------------
    // Le blob = salt(16) | iv(12) | AES-GCM(payload), encodé base64url.
    // La clé est dérivée du mot de passe partagé (PBKDF2-SHA256, 100k itérations).

    encryptConfig: async (password, json) => {
        const salt = crypto.getRandomValues(new Uint8Array(16));
        const iv = crypto.getRandomValues(new Uint8Array(12));
        const key = await mariage._deriveKey(password, salt);
        const ct = new Uint8Array(await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv }, key, new TextEncoder().encode(json)));
        const out = new Uint8Array(16 + 12 + ct.length);
        out.set(salt); out.set(iv, 16); out.set(ct, 28);
        return mariage._b64uEncode(out);
    },

    // Lève une exception si le mot de passe est faux (tag GCM invalide).
    decryptConfig: async (password, blob) => {
        const data = mariage._b64uDecode(blob);
        const key = await mariage._deriveKey(password, data.slice(0, 16));
        const pt = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: data.slice(16, 28) }, key, data.slice(28));
        return new TextDecoder().decode(pt);
    },

    // Dessine le QR code de `text` dans l'élément #id (lib qrcode.min.js).
    makeQr: (id, text) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.innerHTML = '';
        new QRCode(el, { text, width: 220, height: 220, correctLevel: QRCode.CorrectLevel.M });
    },

    _deriveKey: async (password, salt) => {
        const material = await crypto.subtle.importKey(
            'raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveKey']);
        return crypto.subtle.deriveKey(
            { name: 'PBKDF2', salt, iterations: 100000, hash: 'SHA-256' },
            material, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
    },

    _b64uEncode: (bytes) =>
        btoa(String.fromCharCode(...bytes)).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, ''),

    _b64uDecode: (s) => {
        s = s.replaceAll('-', '+').replaceAll('_', '/');
        while (s.length % 4) s += '=';
        return Uint8Array.from(atob(s), c => c.charCodeAt(0));
    }
};
