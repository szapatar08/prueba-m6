## Requerimientos funcionales

### Generales

1. El sistema debe evitar reservas solapadas para un mismo inmueble (double-booking).
2. El sistema debe asignar automáticamente:

   * Check-in: 2:00 PM
   * Check-out: 12:00 PM
3. El sistema debe enviar notificaciones por:

   * Correo electrónico
   * Notificaciones internas
4. El sistema debe generar alertas para:

   * Confirmación de reserva
   * Validación de identidad
   * Recordatorios de llegada/salida

### Usuario (Huésped)

#### Búsqueda y exploración

5. El usuario debe visualizar un catálogo de inmuebles sin autenticarse.
6. El usuario debe filtrar inmuebles por ubicación.
7. El usuario debe filtrar inmuebles por fechas disponibles.

#### Favoritos

8. El usuario debe agregar inmuebles a favoritos.
9. El usuario debe eliminar inmuebles de favoritos.
10. El usuario debe acceder a una lista personalizada de favoritos.
11. El usuario debe comparar o reservar rápidamente inmuebles guardados.

#### Autenticación

12. El sistema debe permitir navegación anónima.
13. El sistema debe solicitar autenticación únicamente al:

* Reservar
* Pagar
* Guardar favoritos permanentes

#### KYC con IA

14. El usuario debe capturar y subir fotografías de su documento de identidad.
15. El sistema debe procesar la imagen mediante IA.
16. El sistema debe extraer:

* Nombres
* Apellidos
* Número de documento
* Fecha de nacimiento

17. El sistema debe emitir un resultado:

* Aprobado
* Rechazado

18. El sistema debe impedir finalizar la primera reserva si el usuario no fue validado.

#### Gestión de reservas

19. El usuario debe visualizar sus reservas.
20. El usuario debe visualizar:

* Fechas
* Estado
* Horarios estándar

### Dueño (Propietario)

#### Gestión de inmuebles

21. El dueño debe publicar nuevos inmuebles.
22. El dueño debe editar inmuebles.
23. El dueño debe subir fotografías.
24. El dueño debe definir tarifas.

#### Dashboard

25. El sistema debe mostrar indicadores de rentabilidad.
26. El sistema debe mostrar tasas de ocupación.
27. El sistema debe mostrar ingresos generados.
28. El sistema debe permitir seleccionar períodos de análisis.

#### Reportes

29. El dueño debe generar reportes Excel (.xlsx).
30. El dueño debe generar reportes para:

* Todo el portafolio
* Un inmueble específico

31. El reporte debe incluir:

* Fechas de alquiler
* Precio pagado
* Datos básicos del usuario
* Inmueble asociado

---

## Requerimientos no funcionales

### Seguridad

1. El sistema debe proteger criptográficamente los documentos de identidad.
2. El sistema debe eliminar de forma segura los documentos cargados.

### Tecnológicos

3. El núcleo y lógica principal deben desarrollarse en .NET 9 o .NET 10.
4. Laravel o Node.js solo pueden utilizarse como complementos secundarios.
5. El sistema debe ejecutarse mediante Docker o Docker Compose.

### Despliegue y mantenimiento

6. El proyecto debe entregarse mediante repositorio Git.
7. El proyecto debe incluir un archivo README.
8. El README debe incluir:

   * Requisitos previos
   * Comandos para levantar Docker
   * Explicación de arquitectura
   * Solución técnica implementada

### Restricciones operativas implícitas del documento

9. El sistema debe operar de forma autónoma mediante automatización de:

   * Disponibilidad
   * Horarios
   * Notificaciones

