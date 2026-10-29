using UnityEngine;

// Marca un campo de texto que lleva el id de un material del ObstacleCatalog, para que el
// Inspector lo ofrezca en un desplegable en vez de obligar a teclearlo.
//
// Un id escrito con un dedazo no falla al guardar: falla en partida, con la burbuja saliendo
// sin caparazón y sin que nada diga por qué.
public class ObstacleIdAttribute : PropertyAttribute { }
