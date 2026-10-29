BEGIN;

-- El tema de cada cuenta (CDU-20). Sin fila, la cuenta sigue el tema de Windows.
CREATE TABLE IF NOT EXISTS preferencias (
    usuario_id integer PRIMARY KEY REFERENCES usuario (id),
    tema varchar(6) NOT NULL
        CHECK (tema IN ('Claro', 'Oscuro'))
);

COMMIT;
