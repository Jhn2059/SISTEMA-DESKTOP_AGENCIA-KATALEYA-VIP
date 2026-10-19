import { opcionesParaRol } from "../../modules/menu/opciones.js";
import { cerrarSesion, leerCuenta, limpiarCuenta } from "../../modules/session/sesion.js";

const cuenta = leerCuenta();

if (!cuenta) {
    window.location.replace("../inicio-sesion/inicio-sesion.html");
} else {
    iniciarPanel(cuenta);
}

function iniciarPanel(datos) {
    const panelUsuario = document.getElementById("panel-usuario");
    const panelRol = document.getElementById("panel-rol");
    const panelSede = document.getElementById("panel-sede");
    const panelTitulo = document.getElementById("panel-titulo");
    const listaOpciones = document.getElementById("lista-opciones");
    const botonCerrarSesion = document.getElementById("boton-cerrar-sesion");

    panelUsuario.textContent = datos.nombre;
    panelRol.textContent = datos.rol;
    panelSede.textContent = datos.sede;
    pintarOpciones(listaOpciones, datos.rol);
    panelTitulo.focus();

    botonCerrarSesion.addEventListener("click", alCerrarSesion);
}

function pintarOpciones(listaOpciones, rol) {
    listaOpciones.replaceChildren();

    const opciones = opcionesParaRol(rol);

    if (opciones.length === 0) {
        const elemento = document.createElement("li");
        elemento.className = "opcion";
        elemento.textContent = "No hay opciones configuradas para este rol.";
        listaOpciones.append(elemento);
        return;
    }

    for (const [nombre, detalle] of opciones) {
        const elemento = document.createElement("li");
        elemento.className = "opcion";

        const titulo = document.createElement("span");
        titulo.className = "opcion__nombre";
        titulo.textContent = nombre;

        const descripcion = document.createElement("span");
        descripcion.className = "opcion__detalle";
        descripcion.textContent = detalle;

        elemento.append(titulo, descripcion);
        listaOpciones.append(elemento);
    }
}

async function alCerrarSesion() {
    const botonCerrarSesion = document.getElementById("boton-cerrar-sesion");
    botonCerrarSesion.disabled = true;

    try {
        await cerrarSesion();
    } catch {
    } finally {
        limpiarCuenta();
        window.location.replace("../inicio-sesion/inicio-sesion.html");
    }
}
