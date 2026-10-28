using UnityEngine;

// Marca un campo de texto que es el id de algo rescatable —una criatura o un objeto— para que el
// Inspector lo ofrezca en un desplegable en vez de pedirlo escrito.
//
// No dice de cuál de los dos catálogos sale, y por eso se llama así: quien escribe el campo pone
// el id a secas, y el desplegable enseña los dos grupos. Resolverlo es cosa de Rescuable.
//
// Va en runtime y no en el editor porque el campo que marca sí es de runtime: el atributo viaja
// con él, y el drawer que lo dibuja vive aparte en Assets/Editor.
public class RescuableIdAttribute : PropertyAttribute { }
