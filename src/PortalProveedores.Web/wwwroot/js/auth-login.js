/**
 * Portal R-Data - Radial Llantas México
 * Script de Microinteracciones y Validaciones para Acceso de Proveedores
 */
document.addEventListener('DOMContentLoaded', () => {
    initPasswordToggle();
    initCapsLockDetector();
    initRfcAutoUppercase();
    initLoginFormState();
});

/**
 * Control interactivo para mostrar / ocultar contraseña
 */
function initPasswordToggle() {
    const toggleBtn = document.getElementById('togglePasswordBtn');
    const passwordInput = document.getElementById('inputPassword');
    const toggleIcon = document.getElementById('togglePasswordIcon');

    if (!toggleBtn || !passwordInput || !toggleIcon) return;

    toggleBtn.addEventListener('click', () => {
        const isPassword = passwordInput.getAttribute('type') === 'password';
        passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
        
        toggleIcon.classList.toggle('bi-eye', !isPassword);
        toggleIcon.classList.toggle('bi-eye-slash', isPassword);
        
        const actionLabel = isPassword ? 'Ocultar contraseña' : 'Ver contraseña';
        toggleBtn.setAttribute('aria-label', actionLabel);
        toggleBtn.setAttribute('title', actionLabel);
        passwordInput.focus();
    });
}

/**
 * Detector reactivo de bloqueo de mayúsculas (Caps Lock)
 */
function initCapsLockDetector() {
    const passwordInput = document.getElementById('inputPassword');
    const warningBadge = document.getElementById('capsLockWarning');

    if (!passwordInput || !warningBadge) return;

    const checkCapsLock = (event) => {
        if (event.getModifierState && event.getModifierState('CapsLock')) {
            warningBadge.classList.remove('d-none');
        } else {
            warningBadge.classList.add('d-none');
        }
    };

    passwordInput.addEventListener('keydown', checkCapsLock);
    passwordInput.addEventListener('keyup', checkCapsLock);
    passwordInput.addEventListener('blur', () => {
        warningBadge.classList.add('d-none');
    });
}

/**
 * Normalización automática de RFC / Usuario a mayúsculas
 */
function initRfcAutoUppercase() {
    const userInput = document.getElementById('inputUsername');
    if (!userInput) return;

    userInput.addEventListener('input', () => {
        const start = userInput.selectionStart;
        const end = userInput.selectionEnd;
        userInput.value = userInput.value.toUpperCase().trimStart();
        userInput.setSelectionRange(start, end);
    });
}

/**
 * Manejo de estado de carga en el botón de submit para prevenir envíos duplicados
 */
function initLoginFormState() {
    const form = document.getElementById('loginForm');
    const submitBtn = document.getElementById('btnLoginSubmit');
    const normalText = document.getElementById('btnSubmitText');
    const loadingText = document.getElementById('btnSubmitLoading');

    if (!form || !submitBtn) return;

    form.addEventListener('submit', (e) => {
        if (!form.checkValidity()) {
            return;
        }

        submitBtn.disabled = true;
        if (normalText && loadingText) {
            normalText.classList.add('d-none');
            loadingText.classList.remove('d-none');
        }
    });
}
