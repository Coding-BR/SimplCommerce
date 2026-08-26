/**
 * Módulo de Efeito Shatter para Blazor
 * Exporta funções para serem consumidas via IJSRuntime
 */

const CONFIG = {
    particleCount: 60,
    sparkCount: 20,
    duration: 1000,
    colors: ['#ff4b2b', '#ff416c', '#8f0e0e'],
    sparkColors: ['#ffffff', '#fff700'],
};

const randomRange = (min, max) => Math.random() * (max - min) + min;
const randomColor = (palette) => palette[Math.floor(Math.random() * palette.length)];

// Cria e anima uma única partícula no DOM
const createParticle = (x, y, type = 'chunk') => {
    const p = document.createElement('div');
    p.classList.add('particle'); // Certifique-se que esta classe existe no CSS global ou isolado

    if (type === 'spark') {
        p.classList.add('spark');
        const size = randomRange(2, 4);
        p.style.width = `${size}px`;
        p.style.height = `${size}px`;
        p.style.background = randomColor(CONFIG.sparkColors);
        p.style.boxShadow = '0 0 10px #fff';
        p.style.borderRadius = '50%';
    } else {
        const size = randomRange(5, 15);
        p.style.width = `${size}px`;
        p.style.height = `${size}px`;
        p.style.background = randomColor(CONFIG.colors);
        p.style.borderRadius = '2px';
    }

    // Posição absoluta baseada na viewport
    p.style.position = 'fixed'; // 'fixed' é mais seguro para overlay em Blazor
    p.style.left = `${x}px`;
    p.style.top = `${y}px`;
    p.style.zIndex = '9999';
    p.style.pointerEvents = 'none';

    document.body.appendChild(p);

    // Física
    const velocityMult = type === 'spark' ? 8 : 4;
    const angle = randomRange(0, Math.PI * 2);
    const velocity = randomRange(50, 200) * (type === 'spark' ? 1.5 : 1);

    const destX = Math.cos(angle) * velocity * velocityMult;
    const destY = Math.sin(angle) * velocity * velocityMult;
    const rotation = randomRange(-720, 720);

    const anim = p.animate([
        { transform: `translate(0, 0) rotate(0deg)`, opacity: 1 },
        { transform: `translate(${destX}px, ${destY}px) rotate(${rotation}deg)`, opacity: 0 }
    ], {
        duration: CONFIG.duration,
        easing: 'cubic-bezier(0.165, 0.84, 0.44, 1)',
        fill: 'forwards'
    });

    anim.onfinish = () => p.remove();
};

/**
 * Função principal chamada pelo Blazor.
 * Retorna uma Promise que resolve quando a "carga" (shake) termina e a explosão acontece.
 */
export function explodeElement(element) {
    return new Promise((resolve) => {
        if (!element) {
            resolve();
            return;
        }

        // 1. Efeito visual de "Carga" antes de explodir
        element.style.animation = "shake 0.2s infinite";
        element.style.background = "#fff";
        element.style.color = "#ff4b2b";
        element.style.transform = "scale(0.95)";

        // 2. Aguarda um pouco (simulando carga)
        setTimeout(() => {
            const rect = element.getBoundingClientRect();
            const centerX = rect.left + rect.width / 2;
            const centerY = rect.top + rect.height / 2;

            // Esconde o elemento original (Blazor vai removê-lo do DOM depois, mas escondemos visualmente agora)
            element.style.opacity = '0';
            element.style.visibility = 'hidden';

            // Gera partículas
            for (let i = 0; i < CONFIG.particleCount; i++) {
                const x = rect.left + randomRange(0, rect.width);
                const y = rect.top + randomRange(0, rect.height);
                createParticle(x, y, 'chunk');
            }

            for (let i = 0; i < CONFIG.sparkCount; i++) {
                createParticle(centerX, centerY, 'spark');
            }

            // Resolve a promise para avisar ao Blazor que pode mudar a tela
            resolve();

        }, 400); // Tempo do efeito de "carga"
    });
}

/**
 * Utilitário para copiar texto (Clipboard API)
 */
export function copyToClipboard(text) {
    return navigator.clipboard.writeText(text);
}
