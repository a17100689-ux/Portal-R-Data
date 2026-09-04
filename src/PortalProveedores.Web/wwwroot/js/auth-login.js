/**
 * Portal R-Data - Radial Llantas México
 * Script de Microinteracciones y Validaciones para Acceso de Proveedores
 */
document.addEventListener('DOMContentLoaded', () => {
    initPasswordToggle();
    initCapsLockDetector();
    initRfcAutoUppercase();
    initLoginFormState();
    initTabFromUrl();
    initProveedorRegistration();
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

    form.addEventListener('submit', () => {
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

/**
 * Activación de pestaña a partir de parámetros de URL (?tab=registro)
 */
function initTabFromUrl() {
    const params = new URLSearchParams(window.location.search);
    const tabParam = params.get('tab');
    if (tabParam === 'registro') {
        const registroBtn = document.getElementById('tab-registro-btn');
        if (registroBtn && typeof bootstrap !== 'undefined' && bootstrap.Tab) {
            const tabInstance = bootstrap.Tab.getOrCreateInstance(registroBtn);
            tabInstance.show();
        }
    }
}

/**
 * Lógica y microinteracciones para el formulario de Alta y Registro de Proveedor
 */
function initProveedorRegistration() {
    const regForm = document.getElementById('registroProveedorForm');
    const regRfc = document.getElementById('regRFC');
    const regRazon = document.getElementById('regRazonSocial');
    const regCodigo = document.getElementById('regCodigoProveedor');
    const regPass = document.getElementById('regPassword');
    const regConfirmPass = document.getElementById('regConfirmPassword');
    const btnGenPass = document.getElementById('btnGenerarPassword');
    const toggleRegPassBtn = document.getElementById('toggleRegPasswordBtn');
    const toggleRegPassIcon = document.getElementById('toggleRegPasswordIcon');
    const btnSubmit = document.getElementById('btnRegistroSubmit');
    const submitText = document.getElementById('btnRegSubmitText');
    const submitLoading = document.getElementById('btnRegSubmitLoading');

    // Auto-uppercase para campos fiscales
    [regRfc, regRazon, regCodigo].forEach(input => {
        if (!input) return;
        input.addEventListener('input', () => {
            const start = input.selectionStart;
            const end = input.selectionEnd;
            input.value = input.value.toUpperCase();
            input.setSelectionRange(start, end);
        });
    });

    // Generador de contraseña segura corporativa (12 caracteres)
    if (btnGenPass && regPass && regConfirmPass) {
        btnGenPass.addEventListener('click', () => {
            const charsUpper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
            const charsLower = 'abcdefghijkmnopqrstuvwxyz';
            const charsNumbers = '23456789';
            const charsSpecial = '!@#$%&*?';
            
            let password = '';
            password += charsUpper.charAt(Math.floor(Math.random() * charsUpper.length));
            password += charsLower.charAt(Math.floor(Math.random() * charsLower.length));
            password += charsNumbers.charAt(Math.floor(Math.random() * charsNumbers.length));
            password += charsSpecial.charAt(Math.floor(Math.random() * charsSpecial.length));

            const allChars = charsUpper + charsLower + charsNumbers + charsSpecial;
            for (let i = 4; i < 12; i++) {
                password += allChars.charAt(Math.floor(Math.random() * allChars.length));
            }

            // Mezclar
            password = password.split('').sort(() => 0.5 - Math.random()).join('');

            regPass.value = password;
            regConfirmPass.value = password;
            regPass.setAttribute('type', 'text');
            regConfirmPass.setAttribute('type', 'text');
            if (toggleRegPassIcon) {
                toggleRegPassIcon.classList.remove('bi-eye');
                toggleRegPassIcon.classList.add('bi-eye-slash');
            }

            // Feedback visual temporal en el botón
            const originalHtml = btnGenPass.innerHTML;
            btnGenPass.innerHTML = '<i class="bi bi-check-circle me-1"></i> ¡Generada!';
            btnGenPass.classList.replace('btn-outline-dark', 'btn-success');
            setTimeout(() => {
                btnGenPass.innerHTML = originalHtml;
                btnGenPass.classList.replace('btn-success', 'btn-outline-dark');
            }, 2000);
        });
    }

    // Toggle ver contraseña en registro
    if (toggleRegPassBtn && regPass) {
        toggleRegPassBtn.addEventListener('click', () => {
            const isPass = regPass.getAttribute('type') === 'password';
            regPass.setAttribute('type', isPass ? 'text' : 'password');
            if (regConfirmPass) {
                regConfirmPass.setAttribute('type', isPass ? 'text' : 'password');
            }
            if (toggleRegPassIcon) {
                toggleRegPassIcon.classList.toggle('bi-eye', !isPass);
                toggleRegPassIcon.classList.toggle('bi-eye-slash', isPass);
            }
        });
    }

    // Estado de carga al enviar alta
    if (regForm && btnSubmit) {
        regForm.addEventListener('submit', () => {
            if (!regForm.checkValidity()) {
                return;
            }
            btnSubmit.disabled = true;
            if (submitText && submitLoading) {
                submitText.classList.add('d-none');
                submitLoading.classList.remove('d-none');
            }
        });
    }
}
