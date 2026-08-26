window.siteJs = {
    initHomeSliders: function () {
        // Init Main Slider (now targeted more specifically if needed, but sticky with slick_slider_activation)
        // Make sure we only target elements that ARE NOT product_slick if they share class
        var $sliderActvation = $('.slick_slider_activation').not('.product_slick');

        if ($sliderActvation.length > 0) {
            if ($sliderActvation.hasClass('slick-initialized')) {
                $sliderActvation.slick('unslick');
            }
            $sliderActvation.slick({
                prevArrow: '<button class="prev_arrow"><i class="icon-arrow-left icons"></i></button>',
                nextArrow: '<button class="next_arrow"><i class="icon-arrow-right icons"></i></button>',
            });
        }
    },

    initProductZoom: function () {
        // Initialize Magnific Popup for product images
        $('.product-zoom-link').magnificPopup({
            type: 'image',
            gallery: {
                enabled: true
            },
            mainClass: 'mfp-fade',
            removalDelay: 160,
            callbacks: {
                open: function () {
                    // Update the image source when popup opens to ensure it's current
                    var activeSrc = $(this.currItem.el).attr('href');
                    this.currItem.src = activeSrc;
                }
            }
        });
    },

    initProductSlider: function () {
        $('.product_slick').each(function () {
            var $this = $(this);
            if (!$this.hasClass('slick-initialized')) {
                $this.slick({
                    slidesToShow: 4,
                    slidesToScroll: 1,
                    arrows: true,
                    // Explicitly provide arrow templates so our CSS can target .prev_arrow / .next_arrow
                    prevArrow: '<button class="prev_arrow"><i class="icon-arrow-left icons"></i></button>',
                    nextArrow: '<button class="next_arrow"><i class="icon-arrow-right icons"></i></button>',
                    dots: false,
                    autoplay: false,
                    speed: 300,
                    infinite: true,
                    responsive: [
                        { "breakpoint": 992, "settings": { "slidesToShow": 3 } },
                        { "breakpoint": 768, "settings": { "slidesToShow": 2 } },
                        { "breakpoint": 300, "settings": { "slidesToShow": 1 } }
                    ]
                });
            }
        });
    },

    dataBackgroundImage: function () {
        jQuery('[data-bgimg]').each(function () {
            var bgImgUrl = jQuery(this).data('bgimg');
            jQuery(this).css({
                'background-image': 'url(' + bgImgUrl + ')',
            });
        });
    },

    initOffcanvasMenu: function () {
        // Remove any existing listeners to avoid duplicates
        document.removeEventListener('click', window._offcanvasHandler);

        // Create handler function
        window._offcanvasHandler = function (e) {
            var target = e.target;

            // Check if clicked on canvas_open or its children
            var canvasOpen = target.closest('.canvas_open');
            if (canvasOpen) {
                e.preventDefault();
                var wrapper = document.querySelector('.offcanvas_menu_wrapper');
                var overlay = document.querySelector('.body_overlay');
                // Toggling 'active' allows closing the menu by clicking the sandwich icon again
                if (wrapper) wrapper.classList.toggle('active');
                if (overlay) overlay.classList.toggle('active');
                return;
            }

            // Check if clicked on canvas_close or its children
            var canvasClose = target.closest('.canvas_close');
            if (canvasClose) {
                e.preventDefault();
                window.siteJs.closeOffcanvasMenu();
                return;
            }

            // Check if clicked on body_overlay
            if (target.classList.contains('body_overlay')) {
                window.siteJs.closeOffcanvasMenu();
                return;
            }

            // Check if clicked on a valid link inside offcanvas menu to auto-close
            var offcanvasLink = target.closest('.offcanvas_main_menu a');
            if (offcanvasLink) {
                var href = offcanvasLink.getAttribute('href');
                if (href && href !== '#' && href !== 'javascript:void(0)') {
                    window.siteJs.closeOffcanvasMenu();
                }
            }
        };

        // Add event listener with event delegation
        document.addEventListener('click', window._offcanvasHandler);
    },

    closeOffcanvasMenu: function () {
        var wrapper = document.querySelector('.offcanvas_menu_wrapper');
        var overlay = document.querySelector('.body_overlay');
        if (wrapper) wrapper.classList.remove('active');
        if (overlay) overlay.classList.remove('active');
    },

    scrollActiveTabIntoView: function () {
        setTimeout(function() {
            var activeTab = document.querySelector('.my-account-tabs .mud-tab-active');
            if (activeTab) {
                activeTab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
            }
        }, 100); // Small delay to ensure DOM update
    }
};

// Copy text to clipboard
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error('Failed to copy text: ', err);
        // Fallback for older browsers
        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        document.body.appendChild(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
            document.body.removeChild(textArea);
            return true;
        } catch (err) {
            document.body.removeChild(textArea);
            return false;
        }
    }
};

// Show toast notification
window.showToast = function (title, message) {
    const container = document.getElementById('toastContainer') || createToastContainer();

    const toast = document.createElement('div');
    toast.className = 'custom-toast success';
    toast.innerHTML = `
        <i class="icon-check icons"></i>
        <div class="toast-content">
            <span class="toast-title">${title}</span>
            <span class="toast-message">${message}</span>
        </div>
    `;

    container.appendChild(toast);

    // Auto remove after 4 seconds (was 12)
    setTimeout(() => {
        toast.classList.add('hide');
        setTimeout(() => {
            if (toast.parentNode === container) {
                container.removeChild(toast);
            }
        }, 300);
    }, 4000);
};

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'toast-container';
    document.body.appendChild(container);
}
