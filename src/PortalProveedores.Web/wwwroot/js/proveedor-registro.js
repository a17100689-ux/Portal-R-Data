/**
 * Portal R-Data - Radial Llantas México
 * Componente: Registro y Alta de Proveedores (Admin)
 * 
 * Responsabilidades:
 * - Búsqueda y verificación reactiva contra el catálogo central ERP (Cat_Proveedores)
 * - Autocompletado inteligente de datos fiscales y comerciales
 * - Buscador interactivo en modal con paginación server-side y debounce
 * - Generador de contraseñas corporativas seguras (cumple estándares SAT y Portal)
 * - Prevención de envíos dobles y microinteracciones de estado
 */

/**
 * @typedef {Object} ProveedorCatalogoDto
 * @property {string} codigoProveedor
 * @property {string} rfc
 * @property {string} razonSocial
 * @property {string} [regimenFiscal]
 * @property {string} [codigoPostal]
 * @property {string|number} [condicionesPago]
 * @property {string} [telefono]
 * @property {string} [email]
 * @property {string} [emailRegistrado]
 * @property {boolean} activo
 * @property {boolean} tieneUsuarioRegistrado
 * @property {boolean} [requiereValidarCompra]
 * @property {boolean} [ordenCompraObligatoria]
 * @property {boolean} [esProveedorNacional]
 */

document.addEventListener('DOMContentLoaded', () => {
    initProveedorRegistration();
    initProveedorBuscadorModal();
});

/**
 * Escapa cadenas HTML para prevenir vulnerabilidades de XSS en renderizado dinámico
 * @param {string} text 
 * @returns {string}
 */
function escapeHtml(text) {
    if (!text) return '';
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return String(text).replace(/[&<>"']/g, m => map[m]);
}

/**
 * Despliega alertas visuales en el contenedor catValidationContainer según el estado de Cat_Proveedores
 * @param {'loading'|'inactivo'|'registrado'|'valido'|'no_encontrado'|'limpiar'} estado 
 * @param {ProveedorCatalogoDto|null} prov 
 * @param {string} [mensajeCustom] 
 */
function mostrarEstadoValidacionCatalogo(estado, prov = null, mensajeCustom = '') {
    const catContainer = document.getElementById('catValidationContainer');
    const btnSubmit = document.getElementById('btnRegistroSubmit');
    if (!catContainer) return;

    catContainer.classList.remove('d-none');

    if (estado === 'loading') {
        catContainer.innerHTML = `
            <div class="alert alert-secondary py-2 px-3 small mb-3 d-flex align-items-center rounded-3 border">
                <span class="spinner-border spinner-border-sm me-2 text-primary" role="status" aria-hidden="true"></span>
                <span>Verificando socio en el Catálogo oficial de Proveedores (Cat_Proveedores)...</span>
            </div>`;
        return;
    }

    if (estado === 'inactivo') {
        catContainer.innerHTML = `
            <div class="alert alert-warning py-2 px-3 small mb-3 d-flex align-items-center rounded-3 border border-warning shadow-sm">
                <i class="bi bi-exclamation-triangle-fill fs-5 me-2 text-warning flex-shrink-0"></i>
                <div>
                    <strong>Proveedor inactivo en ERP:</strong> ${escapeHtml(prov?.razonSocial || prov?.rfc || 'Inactivo')}.<br />
                    No es posible dar de alta en el portal mientras se encuentre inactivo en el sistema central de Radial Llantas.
                </div>
            </div>`;
        if (btnSubmit) btnSubmit.disabled = true;
        return;
    }

    if (estado === 'registrado') {
        catContainer.innerHTML = `
            <div class="alert alert-warning py-2 px-3 small mb-3 d-flex align-items-center rounded-3 border border-warning shadow-sm">
                <i class="bi bi-person-check-fill fs-5 me-2 text-warning flex-shrink-0"></i>
                <div>
                    <strong>Proveedor ya registrado en el portal:</strong> Este proveedor ya cuenta con un usuario activo (${escapeHtml(prov?.emailRegistrado || 'registrado')}).
                    Si requiere restablecer el acceso, utilice la opción de recuperación de contraseña.
                </div>
            </div>`;
        if (btnSubmit) btnSubmit.disabled = true;
        return;
    }

    if (estado === 'valido') {
        catContainer.innerHTML = `
            <div class="alert alert-success py-2 px-3 small mb-3 d-flex align-items-center rounded-3 border border-success shadow-sm">
                <i class="bi bi-check-circle-fill fs-5 me-2 text-success flex-shrink-0"></i>
                <div>
                    <strong>Socio Comercial Verificado:</strong> ${escapeHtml(prov?.razonSocial)} (Código: <span class="font-monospace">${escapeHtml(prov?.codigoProveedor)}</span>).
                    <div class="text-muted" style="font-size: 0.72rem;">Código Postal, Correo, Teléfono y Políticas Comerciales precargados automáticamente del ERP. Puede modificarlos si lo desea antes de dar de alta.</div>
                </div>
            </div>`;
        if (btnSubmit) btnSubmit.disabled = false;
        return;
    }

    if (estado === 'no_encontrado') {
        catContainer.innerHTML = `
            <div class="alert alert-danger py-2 px-3 small mb-3 d-flex align-items-center rounded-3 border border-danger shadow-sm">
                <i class="bi bi-x-circle-fill fs-5 me-2 text-danger flex-shrink-0"></i>
                <div>
                    <strong>No encontrado en Cat_Proveedores:</strong> ${escapeHtml(mensajeCustom || 'El Código o RFC no existe en el catálogo oficial de Radial Llantas. Por regla de seguridad, ningún proveedor puede ser registrado sin alta previa en el ERP central.')}
                </div>
            </div>`;
        if (btnSubmit) btnSubmit.disabled = true;
        return;
    }

    if (estado === 'limpiar') {
        catContainer.classList.add('d-none');
        catContainer.innerHTML = '';
        if (btnSubmit) btnSubmit.disabled = false;
    }
}

/**
 * Autocompleta los campos del formulario a partir de los datos obtenidos de Cat_Proveedores
 * @param {ProveedorCatalogoDto} prov 
 */
function autocompletarFormularioProveedor(prov) {
    if (!prov) return;

    const regCodigo = document.getElementById('regCodigoProveedor');
    const regRfc = document.getElementById('regRFC');
    const badgeRfcAutollenado = document.getElementById('badgeRfcAutollenado');
    const regRazon = document.getElementById('regRazonSocial');
    const regCondiciones = document.getElementById('regCondicionesPago');
    const chkValidarCompra = document.getElementById('chkValidarCompra');
    const chkOCObligatoria = document.getElementById('chkOCObligatoria');
    const chkNacional = document.getElementById('chkNacional');

    if (regCodigo && prov.codigoProveedor) {
        regCodigo.value = prov.codigoProveedor;
    }
    if (regRfc && prov.rfc) {
        regRfc.value = prov.rfc;
        if (badgeRfcAutollenado) badgeRfcAutollenado.classList.remove('d-none');
    }
    if (regRazon && prov.razonSocial) {
        regRazon.value = prov.razonSocial;
    }
    if (regCondiciones && prov.condicionesPago !== undefined && prov.condicionesPago !== null) {
        const val = String(prov.condicionesPago).trim();
        if (regCondiciones.querySelector(`option[value="${val}"]`)) {
            regCondiciones.value = val;
        }
    }
    if (chkValidarCompra && prov.requiereValidarCompra !== undefined) {
        chkValidarCompra.checked = Boolean(prov.requiereValidarCompra);
    }
    if (chkOCObligatoria && prov.ordenCompraObligatoria !== undefined) {
        chkOCObligatoria.checked = Boolean(prov.ordenCompraObligatoria);
    }
    if (chkNacional && prov.esProveedorNacional !== undefined) {
        chkNacional.checked = Boolean(prov.esProveedorNacional);
    }

    const regCp = document.getElementById('regCodigoPostal');
    const regTel = document.getElementById('regTelefono');
    const regEmail = document.getElementById('regEmail');
    const regRegimen = document.getElementById('regRegimenFiscal');
    const badgeCp = document.getElementById('badgeCpAutollenado');
    const badgeTel = document.getElementById('badgeTelAutollenado');
    const badgeEmail = document.getElementById('badgeEmailAutollenado');

    if (regCp) {
        if (prov.codigoPostal) {
            regCp.value = String(prov.codigoPostal).trim();
            if (badgeCp) badgeCp.classList.remove('d-none');
        } else {
            if (badgeCp) badgeCp.classList.add('d-none');
        }
    }

    if (regTel) {
        if (prov.telefono) {
            regTel.value = String(prov.telefono).trim();
            if (badgeTel) badgeTel.classList.remove('d-none');
        } else {
            if (badgeTel) badgeTel.classList.add('d-none');
        }
    }

    const emailAUsar = prov.emailContacto || prov.email;
    if (regEmail) {
        if (emailAUsar && !prov.tieneUsuarioRegistrado) {
            regEmail.value = String(emailAUsar).trim().toLowerCase();
            if (badgeEmail) badgeEmail.classList.remove('d-none');
        } else {
            if (badgeEmail) badgeEmail.classList.add('d-none');
        }
    }

    if (regRegimen && prov.regimenFiscal) {
        const regVal = String(prov.regimenFiscal).trim();
        if (regRegimen.querySelector(`option[value="${regVal}"]`)) {
            regRegimen.value = regVal;
        }
    }
}

/**
 * Inicializa los controladores e interacciones del formulario de registro
 */
function initProveedorRegistration() {
    const regForm = document.getElementById('registroProveedorForm');
    const regRfc = document.getElementById('regRFC');
    const regRazon = document.getElementById('regRazonSocial');
    const regCodigo = document.getElementById('regCodigoProveedor');
    const btnBuscarCodigo = document.getElementById('btnBuscarCodigo');
    const iconBuscarCodigo = document.getElementById('iconBuscarCodigo');
    const regPass = document.getElementById('regPassword');
    const regConfirmPass = document.getElementById('regConfirmPassword');
    const btnGenPass = document.getElementById('btnGenerarPassword');
    const toggleRegPassBtn = document.getElementById('toggleRegPasswordBtn');
    const toggleRegPassIcon = document.getElementById('toggleRegPasswordIcon');
    const btnSubmit = document.getElementById('btnRegistroSubmit');
    const submitText = document.getElementById('btnRegSubmitText');
    const submitLoading = document.getElementById('btnRegSubmitLoading');

    // Normalización a mayúsculas
    [regRfc, regRazon, regCodigo].forEach(input => {
        if (!input) return;
        input.addEventListener('input', () => {
            const start = input.selectionStart;
            const end = input.selectionEnd;
            input.value = input.value.toUpperCase();
            input.setSelectionRange(start, end);
        });
    });

    // Generador de contraseñas corporativas robustas (12 caracteres: Mayús + Minús + Números + Símbolos)
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

            password = password.split('').sort(() => 0.5 - Math.random()).join('');

            regPass.value = password;
            regConfirmPass.value = password;
            regPass.setAttribute('type', 'text');
            regConfirmPass.setAttribute('type', 'text');

            if (toggleRegPassIcon) {
                toggleRegPassIcon.classList.remove('bi-eye');
                toggleRegPassIcon.classList.add('bi-eye-slash');
            }

            const originalHtml = btnGenPass.innerHTML;
            btnGenPass.innerHTML = '<i class="bi bi-check2-circle me-1"></i> ¡Generada!';
            btnGenPass.classList.replace('btn-outline-dark', 'btn-success');
            setTimeout(() => {
                btnGenPass.innerHTML = originalHtml;
                btnGenPass.classList.replace('btn-success', 'btn-outline-dark');
            }, 2200);
        });
    }

    // Toggle de visibilidad de contraseña
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

    // Verificación asíncrona contra Cat_Proveedores
    let debounceTimer = null;

    const verificarCatalogoAsync = async () => {
        const codigo = (regCodigo?.value || '').trim();
        const rfc = (regRfc?.value || '').trim();

        if (codigo.length < 3 && rfc.length < 12) {
            mostrarEstadoValidacionCatalogo('limpiar');
            return;
        }

        if (iconBuscarCodigo) {
            iconBuscarCodigo.classList.add('bi-spin');
        }

        mostrarEstadoValidacionCatalogo('loading');

        try {
            const url = `/api/v1/proveedores/catalogo/verificar?codigoProveedor=${encodeURIComponent(codigo)}&rfc=${encodeURIComponent(rfc)}`;
            const resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
            const json = await resp.json();

            if (json.success && json.data && json.data.enCatalogo) {
                const prov = json.data;
                autocompletarFormularioProveedor(prov);

                if (!prov.activo) {
                    mostrarEstadoValidacionCatalogo('inactivo', prov);
                } else if (prov.tieneUsuarioRegistrado) {
                    mostrarEstadoValidacionCatalogo('registrado', prov);
                } else {
                    mostrarEstadoValidacionCatalogo('valido', prov);
                }
            } else {
                mostrarEstadoValidacionCatalogo('no_encontrado', null, json?.message);
            }
        } catch (err) {
            console.error('Error al consultar Cat_Proveedores:', err);
            mostrarEstadoValidacionCatalogo('limpiar');
        } finally {
            if (iconBuscarCodigo) {
                iconBuscarCodigo.classList.remove('bi-spin');
            }
        }
    };

    if (btnBuscarCodigo) {
        btnBuscarCodigo.addEventListener('click', () => {
            verificarCatalogoAsync();
        });
    }

    if (regCodigo) {
        regCodigo.addEventListener('blur', () => {
            if (regCodigo.value.trim().length >= 3) {
                verificarCatalogoAsync();
            }
        });

        regCodigo.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                verificarCatalogoAsync();
            }
        });
    }

    if (regRfc) {
        regRfc.addEventListener('blur', () => {
            if (regRfc.value.trim().length >= 12) {
                verificarCatalogoAsync();
            }
        });
    }

    [regCodigo, regRfc].forEach(input => {
        if (!input) return;
        input.addEventListener('input', () => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                if (input.value.trim().length >= 4) {
                    verificarCatalogoAsync();
                }
            }, 600);
        });
    });

    // Envío del formulario y prevención de envíos dobles
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

/**
 * Control del Modal Buscador de Proveedores en Catálogo ERP
 */
function initProveedorBuscadorModal() {
    const modalEl = document.getElementById('modalBuscadorCatalogo');
    const inputBuscar = document.getElementById('inputBuscarCatalogo');
    const btnLimpiar = document.getElementById('btnLimpiarBusquedaCatalogo');
    const spinner = document.getElementById('spinnerBuscadorCatalogo');
    const spinnerText = document.getElementById('spinnerTextBuscador');
    const contenedorTabla = document.getElementById('contenedorResultadosCatalogo');
    const tbody = document.getElementById('tbodyResultadosCatalogo');
    const estadoVacio = document.getElementById('estadoVacioCatalogo');
    const txtResumen = document.getElementById('txtResumenPaginacionCatalogo');
    const btnAnterior = document.getElementById('btnPaginaAnteriorCatalogo');
    const btnSiguiente = document.getElementById('btnPaginaSiguienteCatalogo');
    const regEmail = document.getElementById('regEmail');

    // Elementos de sincronización manual con ERP
    const btnSincronizarModal = document.getElementById('btnSincronizarCatalogoModal');
    const iconSync = document.getElementById('iconSyncModal');
    const txtSync = document.getElementById('txtSyncModal');
    const alertSync = document.getElementById('alertSyncModal');
    const alertSyncTexto = document.getElementById('alertSyncModalTexto');
    const alertSyncIcon = document.getElementById('alertSyncModalIcon');
    const btnSyncVacio = document.getElementById('btnSyncDesdeEstadoVacio');

    if (!modalEl || !inputBuscar || !tbody) return;

    let paginaActual = 1;
    const tamanoPagina = 10;
    let terminoActual = '';
    let debounceTimer = null;
    let totalPaginas = 1;

    /**
     * Sincroniza el catálogo completo con Punto_de_Venta (ERP) bajo demanda
     */
    const sincronizarCatalogoConErpAsync = async () => {
        if (btnSincronizarModal) btnSincronizarModal.disabled = true;
        if (btnSyncVacio) btnSyncVacio.disabled = true;
        if (iconSync) iconSync.className = 'spinner-border spinner-border-sm me-1';
        if (txtSync) txtSync.textContent = 'Sincronizando...';
        if (spinnerText) spinnerText.textContent = 'Sincronizando proveedores directamente desde Punto de Venta (ERP)...';
        if (spinner) spinner.classList.remove('d-none');
        if (contenedorTabla) contenedorTabla.classList.add('d-none');
        if (estadoVacio) estadoVacio.classList.add('d-none');
        if (alertSync) alertSync.classList.add('d-none');

        try {
            const resp = await fetch('/api/v1/proveedores/catalogo/sincronizar', {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                }
            });
            const json = await resp.json();

            if (alertSync && alertSyncTexto) {
                alertSync.classList.remove('d-none');
                if (json.success) {
                    alertSync.className = 'alert alert-success py-2 px-3 small rounded-3 mb-3 shadow-sm d-flex align-items-center';
                    if (alertSyncIcon) alertSyncIcon.className = 'bi bi-check-circle-fill text-success fs-5 me-2 flex-shrink-0';
                    alertSyncTexto.textContent = json.message || 'Catálogo sincronizado exitosamente con el ERP Central.';
                } else {
                    alertSync.className = 'alert alert-warning py-2 px-3 small rounded-3 mb-3 shadow-sm d-flex align-items-center';
                    if (alertSyncIcon) alertSyncIcon.className = 'bi bi-exclamation-triangle-fill text-warning fs-5 me-2 flex-shrink-0';
                    alertSyncTexto.textContent = json.message || 'Advertencia durante la sincronización con el ERP.';
                }
            }

            // Recargar búsqueda con la información fresca
            await buscarProveedoresEnCatalogo(1);
        } catch (err) {
            console.error('Error al sincronizar catálogo con ERP:', err);
            if (alertSync && alertSyncTexto) {
                alertSync.classList.remove('d-none');
                alertSync.className = 'alert alert-danger py-2 px-3 small rounded-3 mb-3 shadow-sm d-flex align-items-center';
                if (alertSyncIcon) alertSyncIcon.className = 'bi bi-x-circle-fill text-danger fs-5 me-2 flex-shrink-0';
                alertSyncTexto.textContent = 'Error al comunicarse con el servidor para sincronizar catálogo.';
            }
            if (spinner) spinner.classList.add('d-none');
            if (contenedorTabla) contenedorTabla.classList.remove('d-none');
        } finally {
            if (btnSincronizarModal) btnSincronizarModal.disabled = false;
            if (btnSyncVacio) btnSyncVacio.disabled = false;
            if (iconSync) iconSync.className = 'bi bi-arrow-repeat me-1';
            if (txtSync) txtSync.textContent = 'Refrescar ERP';
            if (spinnerText) spinnerText.textContent = 'Consultando base central Cat_Proveedores...';
        }
    };

    if (btnSincronizarModal) {
        btnSincronizarModal.addEventListener('click', sincronizarCatalogoConErpAsync);
    }
    if (btnSyncVacio) {
        btnSyncVacio.addEventListener('click', sincronizarCatalogoConErpAsync);
    }

    const buscarProveedoresEnCatalogo = async (pagina = 1) => {
        paginaActual = pagina;
        if (spinner) spinner.classList.remove('d-none');
        if (contenedorTabla) contenedorTabla.classList.add('d-none');
        if (estadoVacio) estadoVacio.classList.add('d-none');

        try {
            const url = `/api/v1/proveedores/catalogo/buscar?termino=${encodeURIComponent(terminoActual)}&pagina=${paginaActual}&tamanoPagina=${tamanoPagina}`;
            const resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
            const json = await resp.json();

            if (spinner) spinner.classList.add('d-none');

            if (json.success && json.data && json.data.items && json.data.items.length > 0) {
                const data = json.data;
                totalPaginas = data.totalPages || 1;

                tbody.innerHTML = '';
                data.items.forEach(prov => {
                    const tr = document.createElement('tr');

                    let badgeEstado = '';
                    let botonAccion = '';

                    if (prov.tieneUsuarioRegistrado) {
                        badgeEstado = `<span class="badge bg-secondary"><i class="bi bi-person-check-fill me-1"></i>Registrado</span>`;
                        botonAccion = `<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2 disabled" style="font-size: 0.75rem;" title="El proveedor ya cuenta con un usuario activo">Registrado</button>`;
                    } else if (!prov.activo) {
                        badgeEstado = `<span class="badge bg-danger"><i class="bi bi-x-circle me-1"></i>Inactivo ERP</span>`;
                        botonAccion = `<button type="button" class="btn btn-sm btn-outline-danger py-0 px-2 disabled" style="font-size: 0.75rem;" title="Inactivo en el ERP central">Inactivo</button>`;
                    } else {
                        badgeEstado = `<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-check-circle me-1"></i>Disponible</span>`;
                        botonAccion = `<button type="button" class="btn btn-sm btn-radial py-0 px-2 fw-semibold btn-seleccionar-proveedor" style="font-size: 0.75rem;"><i class="bi bi-check2 me-1"></i>Seleccionar</button>`;
                    }

                    tr.innerHTML = `
                        <td><span class="badge bg-light text-dark border font-monospace fw-bold">${escapeHtml(prov.codigoProveedor)}</span></td>
                        <td><span class="font-monospace fw-semibold">${escapeHtml(prov.rfc)}</span></td>
                        <td>
                            <div class="fw-semibold text-dark">${escapeHtml(prov.razonSocial)}</div>
                            <div class="text-muted" style="font-size: 0.72rem;">
                                ${prov.codigoPostal ? `<span class="me-2"><i class="bi bi-geo-alt"></i> CP: ${escapeHtml(prov.codigoPostal)}</span>` : ''}
                                ${prov.telefono ? `<span class="me-2"><i class="bi bi-telephone"></i> ${escapeHtml(prov.telefono)}</span>` : ''}
                                ${prov.emailContacto ? `<span><i class="bi bi-envelope"></i> ${escapeHtml(prov.emailContacto)}</span>` : ''}
                            </div>
                        </td>
                        <td class="text-center">${badgeEstado}</td>
                        <td class="text-end">${botonAccion}</td>
                    `;

                    const btnSelect = tr.querySelector('.btn-seleccionar-proveedor');
                    if (btnSelect) {
                        btnSelect.addEventListener('click', () => {
                            autocompletarFormularioProveedor(prov);
                            mostrarEstadoValidacionCatalogo('valido', prov);

                            if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
                                const modalInstance = bootstrap.Modal.getInstance(modalEl);
                                if (modalInstance) modalInstance.hide();
                            }

                            setTimeout(() => {
                                if (regEmail) regEmail.focus();
                            }, 300);
                        });
                    }

                    tbody.appendChild(tr);
                });

                if (contenedorTabla) contenedorTabla.classList.remove('d-none');
                if (txtResumen) {
                    txtResumen.textContent = `Página ${data.pageNumber} de ${data.totalPages} (${data.totalRecords} proveedores)`;
                }
                if (btnAnterior) btnAnterior.disabled = !data.hasPreviousPage;
                if (btnSiguiente) btnSiguiente.disabled = !data.hasNextPage;

            } else {
                if (estadoVacio) estadoVacio.classList.remove('d-none');
                tbody.innerHTML = '';
                if (txtResumen) txtResumen.textContent = '0 proveedores encontrados';
                if (btnAnterior) btnAnterior.disabled = true;
                if (btnSiguiente) btnSiguiente.disabled = true;
            }

        } catch (err) {
            console.error('Error al buscar en catálogo:', err);
            if (spinner) spinner.classList.add('d-none');
            if (estadoVacio) estadoVacio.classList.remove('d-none');
            if (btnAnterior) btnAnterior.disabled = true;
            if (btnSiguiente) btnSiguiente.disabled = true;
        }
    };

    modalEl.addEventListener('shown.bs.modal', () => {
        inputBuscar.focus();
        if (tbody.children.length === 0) {
            buscarProveedoresEnCatalogo(1);
        }
    });

    inputBuscar.addEventListener('input', () => {
        terminoActual = inputBuscar.value.trim();
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            buscarProveedoresEnCatalogo(1);
        }, 300);
    });

    if (btnLimpiar) {
        btnLimpiar.addEventListener('click', () => {
            inputBuscar.value = '';
            terminoActual = '';
            buscarProveedoresEnCatalogo(1);
            inputBuscar.focus();
        });
    }

    if (btnAnterior) {
        btnAnterior.addEventListener('click', () => {
            if (paginaActual > 1) {
                buscarProveedoresEnCatalogo(paginaActual - 1);
            }
        });
    }

    if (btnSiguiente) {
        btnSiguiente.addEventListener('click', () => {
            if (paginaActual < totalPaginas) {
                buscarProveedoresEnCatalogo(paginaActual + 1);
            }
        });
    }
}
