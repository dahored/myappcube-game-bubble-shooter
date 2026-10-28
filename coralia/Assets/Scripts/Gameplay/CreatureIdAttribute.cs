using UnityEngine;

// Marca un campo de texto que es el id de una criatura o de un objeto del CreatureCatalog, para
// que el Inspector lo ofrezca en un desplegable en vez de pedirlo escrito.
//
// Va en runtime y no en el editor porque el campo que marca sí es de runtime: el atributo viaja
// con él, y el drawer que lo dibuja vive aparte en Assets/Editor.
public class CreatureIdAttribute : PropertyAttribute { }
