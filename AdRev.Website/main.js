// AdRev Science - Premium Interactivity System
document.addEventListener('DOMContentLoaded', () => {

    // 1. Scroll-reveal Observer for all animated elements
    const revealOptions = {
        threshold: 0.15,
        rootMargin: "0px 0px -100px 0px"
    };

    const revealObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('revealed');
            }
        });
    }, revealOptions);

    // Initial state setup for reveal elements
    document.querySelectorAll('.animate-on-scroll, .glass-card, .price-card, .process-item, .hero-text, .hero-visual').forEach(el => {
        el.classList.add('reveal-init');
        revealObserver.observe(el);
    });

    // 2. Sophisticated Header Scroll Effect
    const header = document.querySelector('.glass-nav');
    window.addEventListener('scroll', () => {
        if (window.scrollY > 50) {
            header.style.top = '1rem';
            header.style.width = '95%';
            header.style.background = 'rgba(2, 6, 23, 0.9)';
            header.style.boxShadow = '0 20px 40px rgba(0,0,0,0.3)';
        } else {
            header.style.top = '2rem';
            header.style.width = '85%';
            header.style.background = 'rgba(15, 23, 42, 0.6)';
            header.style.boxShadow = 'none';
        }
    });

    // 3. Smooth Anchor Scrolling with offset
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;

            const target = document.querySelector(targetId);
            if (target) {
                e.preventDefault();
                const navHeight = 90;
                const elementPosition = target.getBoundingClientRect().top;
                const offsetPosition = elementPosition + window.pageYOffset - navHeight;

                window.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });

    // 4. Subtle Parallax for Hero
    window.addEventListener('scroll', () => {
        const scrolled = window.scrollY;
        const heroVisual = document.querySelector('.hero-visual');
        if (heroVisual && scrolled < 1000) {
            heroVisual.style.transform = `translateY(${scrolled * 0.1}px)`;
        }
    });

    // 5. Newsletter Mockup Interaction
    const newsletterForm = document.querySelector('.newsletter-form');
    if (newsletterForm) {
        newsletterForm.addEventListener('submit', (e) => {
            e.preventDefault();
            const input = newsletterForm.querySelector('input');
            const button = newsletterForm.querySelector('button');
            if (input.value) {
                const originalText = button.textContent;
                button.textContent = '✓';
                button.style.background = 'var(--secondary)';
                input.value = '';
                setTimeout(() => {
                    button.textContent = originalText;
                    button.style.background = 'var(--primary)';
                }, 3000);
            }
        });
    }

    // 6. Automatic Currency Detection Logic
    const priceValues = document.querySelectorAll('.price-val');
    const currSymbols = document.querySelectorAll('.curr-symbol');
    let activeCurrency = 'USD';

    const rates = {
        XOF: { rate: 1, symbol: ' FCFA' },
        USD: { rate: 1/610, symbol: ' $' }, // Approx base 35k FCFA -> 57$
        EUR: { rate: 1/655.957, symbol: ' €' }
    };

    function updatePrices(currencyCode) {
        let selected = rates[currencyCode];
        
        // Map XAF (Central Africa) to XOF (West Africa) as they are parity
        if (currencyCode === 'XAF') selected = rates['XOF'];
        
        // Fallback to USD if currency not supported
        if (!selected) {
            selected = rates['USD'];
            currencyCode = 'USD';
        }

        activeCurrency = currencyCode;

        priceValues.forEach(priceEl => {
            const basePrice = parseFloat(priceEl.getAttribute('data-base'));
            const converted = basePrice * selected.rate;
            
            let formatted;
            if (currencyCode === 'XOF' || currencyCode === 'XAF') {
                formatted = new Intl.NumberFormat('fr-FR').format(Math.round(converted / 100) * 100); // Round to nearest 100
            } else {
                formatted = new Intl.NumberFormat('en-US', {
                    minimumFractionDigits: 0,
                    maximumFractionDigits: 0
                }).format(converted);
            }
            
            priceEl.textContent = formatted;
        });

        currSymbols.forEach(symbolEl => {
            symbolEl.textContent = selected.symbol;
        });
    }

    async function detectAndApplyCurrency() {
        // 1. Check Cache
        const cached = localStorage.getItem('adrev_currency');
        if (cached) {
            updatePrices(cached);
            return;
        }

        // 2. Fetch from Geolocation API (ipapi.co is reliable)
        try {
            const response = await fetch('https://ipapi.co/json/');
            const data = await response.json();
            const currency = data.currency || 'USD';
            
            localStorage.setItem('adrev_currency', currency);
            updatePrices(currency);
        } catch (error) {
            console.warn('Geolocation failed, defaulting to USD');
            updatePrices('USD');
        }
    }

    // 7. Direct WhatsApp Purchase Bridge (+22379276470)
    // Facilitates manual payments via Orange Money/Moov as requested.
    window.makePayment = function(amountCFA, planName) {
        const phoneNumber = "22379276470";
        const message = encodeURIComponent(
            `Bonjour AdRev Science ! 🔬🚀\n\n` +
            `Je souhaite souscrire au plan *${planName}* (${amountCFA.toLocaleString()} FCFA/an).\n\n` +
            `Pouvez-vous m'envoyer les instructions pour le paiement par Orange Money / Moov Money afin d'activer ma licence ?`
        );
        
        const whatsappUrl = `https://wa.me/${phoneNumber}?text=${message}`;
        
        // Open WhatsApp in a new tab
        window.open(whatsappUrl, '_blank');
    }

    // Initialize with default (USD) while loading if needed, then auto-detect
    updatePrices('USD');
    detectAndApplyCurrency();

    console.log('AdRev Premium Site Core Restructured 🚀');
});
