"""Descifra una copia semanal de Kataleya y deja el .zip con un CSV por tabla.

Uso: python descifrar-copia.py kataleya-copia-2026-10-25-0301.cifrada "clave de cifrado"
Necesita: pip install cryptography
"""
import hashlib
import sys

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

CABECERA = b"KTLY1"
ITERACIONES = 200_000


def descifrar(contenido: bytes, clave: str) -> bytes:
    if not contenido.startswith(CABECERA):
        raise ValueError("El archivo no es una copia de Kataleya.")
    resto = contenido[len(CABECERA):]
    sal, nonce, etiqueta, cifrado = resto[:16], resto[16:28], resto[28:44], resto[44:]
    llave = hashlib.pbkdf2_hmac("sha256", clave.encode("utf-8"), sal, ITERACIONES, 32)
    return AESGCM(llave).decrypt(nonce, cifrado + etiqueta, None)


if __name__ == "__main__":
    archivo, clave = sys.argv[1], sys.argv[2]
    with open(archivo, "rb") as entrada:
        datos = descifrar(entrada.read(), clave)
    destino = archivo.rsplit(".", 1)[0] + ".zip"
    with open(destino, "wb") as salida:
        salida.write(datos)
    print(f"Copia descifrada en {destino}")
