# Proyecto-Feria-de-Ciencias
Proyecto Feria de Ciencias Pablo Prato y Omar Correa

The Last Guest VR

Escape room de terror en Realidad Virtual, desarrollado para la Feria de Ciencias. Ambientado en un mapa estilo Backrooms, el jugador debe resolver un puzle de símbolos y palancas mientras evita ser detectado por una criatura que acecha el mapa — todo controlado sin joysticks ni controles físicos, únicamente con la mirada.

Plataforma
Motor: Unity 6 (C#)
Dispositivo: VR móvil (Android) con visores tipo Google Cardboard
Input: 100% por interacción de mirada (Gaze Interaction) — sin controllers, sin hand-tracking
Objetivo de rendimiento: 60 FPS en móvil
Duración de partida: 3 a 5 minutos (pensado para rotación de público en la feria)
Mecánicas principales
Sistema de mirada (Gaze System)

Una retícula central detecta objetos interactuables mediante raycast. Al sostener la mirada sobre un objeto entre 1.5 y 2 segundos, se completa la interacción (activar palanca, avanzar por un waypoint, etc).

Navegación

Sin movimiento libre caminando (evita mareos). El desplazamiento es por nodos de teletransporte: el jugador mira un punto en el suelo y la cámara se desplaza suavemente hacia esa posición.

Puzle

El jugador debe encontrar 3 símbolos ocultos en las paredes de las habitaciones y luego activar 3 palancas en la secuencia correcta, usando la mirada, para desbloquear la puerta de salida.

Enemigo

Hasta ahora tenemos un enemigo que fu funcion es silbarte o hacerte un ruido en el oido y cuando el jugador escucha ese ruido caracteristico tiene q mirar para todas las dirrecciones porque este enemigo lo va a estar mirando y lo tiene que sacar con la mirada, y si no lo mira lo mata, y en el futuro pensamos poner alguno mas.

Audio

Audio ambiental constante más efectos 3D espaciales en la posición de la criatura, para que el jugador pueda ubicarla por dirección del sonido al girar la cabeza.
