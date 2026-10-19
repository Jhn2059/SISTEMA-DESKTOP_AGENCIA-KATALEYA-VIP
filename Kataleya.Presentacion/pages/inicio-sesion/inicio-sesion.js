import { puente } from "../../modules/core/puente.js";
import { MENSAJE_SIN_PUENTE, mensajeDeError, mensajeDeRechazo } from "../../modules/shared/mensajes.js";
import { guardarCuenta, iniciarSesion } from "../../modules/session/sesion.js";

const formulario = document.getElementById("formulario-inicio");
const nombreUsuario = document.getElementById("nombre-usuario");
const contrasena = document.getElementById("contrasena");
const errorNombreUsuario = document.getElementById("error-nombre-usuario");
const errorContrasena = document.getElementById("error-contrasena");
const avisoBloqMayus = document.getElementById("aviso-bloq-mayus");
const mensajeInicio = document.getElementById("mensaje-inicio");
const botonIngresar = document.getElementById("boton-ingresar");

if (!puente.disponible()) {
    mostrarMensaje(MENSAJE_SIN_PUENTE);
    botonIngresar.disabled = true;
}

formulario.addEventListener("submit", alEnviar);

contrasena.addEventListener("keydown", actualizarBloqMayus);
contrasena.addEventListener("keyup", actualizarBloqMayus);
contrasena.addEventListener("blur", () => {
    avisoBloqMayus.hidden = true;
});

nombreUsuario.addEventListener("input", () => {
    limpiarErrorDeCampo(nombreUsuario, errorNombreUsuario);
    ocultarMensaje();
});

contrasena.addEventListener("input", () => {
    limpiarErrorDeCampo(contrasena, errorContrasena);
    ocultarMensaje();
});

async function alEnviar(evento) {
    evento.preventDefault();
    ocultarMensaje();
    avisoBloqMayus.hidden = true;

    const usuario = nombreUsuario.value.trim();
    const clave = contrasena.value;

    if (!validarCampos(usuario, clave)) {
        return;
    }

    bloquearIngreso(true);

    try {
        const resultado = await iniciarSesion(usuario, clave);

        if (resultado?.cuenta) {
            guardarCuenta(resultado.cuenta);
            window.location.href = "../panel/panel.html";
            return;
        }

        mostrarRechazo(resultado?.rechazo, resultado?.finBloqueo);
    } catch (error) {
        mostrarMensaje(mensajeDeError(error));
    } finally {
        bloquearIngreso(false);
    }
}

function validarCampos(usuario, clave) {
    let valido = true;

    if (!usuario) {
        mostrarErrorDeCampo(
            nombreUsuario,
            errorNombreUsuario,
            "Escriba su nombre de usuario.");
        valido = false;
    }

    if (!clave) {
        mostrarErrorDeCampo(contrasena, errorContrasena, "Escriba su contraseña.");
        valido = false;
    }

    return valido;
}

function mostrarRechazo(rechazo, finBloqueo) {
    mostrarMensaje(mensajeDeRechazo(rechazo, finBloqueo));

    contrasena.value = "";
    contrasena.focus();
}

function mostrarErrorDeCampo(entrada, elemento, texto) {
    entrada.setAttribute("aria-invalid", "true");
    elemento.textContent = texto;
}

function limpiarErrorDeCampo(entrada, elemento) {
    entrada.removeAttribute("aria-invalid");
    elemento.textContent = "";
}

function mostrarMensaje(texto) {
    mensajeInicio.textContent = texto;
    mensajeInicio.hidden = false;
}

function ocultarMensaje() {
    mensajeInicio.textContent = "";
    mensajeInicio.hidden = true;
}

function bloquearIngreso(bloqueado) {
    botonIngresar.disabled = bloqueado;
    botonIngresar.textContent = bloqueado ? "Ingresando..." : "Ingresar";
    formulario.setAttribute("aria-busy", String(bloqueado));
}

function actualizarBloqMayus(evento) {
    if (typeof evento.getModifierState !== "function") {
        return;
    }

    avisoBloqMayus.hidden = !evento.getModifierState("CapsLock");
}

nombreUsuario.focus();
