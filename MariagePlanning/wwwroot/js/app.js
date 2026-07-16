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
    }
};
