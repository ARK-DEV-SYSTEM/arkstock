# ARKSTOCK v1.0.12

**ARKSTOCK** es un sistema moderno de inventario y punto de venta (POS) diseñado para micro y pequeñas empresas. Desarrollado por **ARK DEV SYSTEM**, este software es **Open Source** y está pensado para ser utilizado de forma gratuita por la comunidad.

> **Desarrollado por:** Joel Andrés (ARK DEV SYSTEM)
> **Licencia:** Código Libre (Open Source)
> **Web:** [https://www.ark-dev.org/ark_dev_system](https://www.ark-dev.org/ark_dev_system)

Todo el sistema se ejecuta de manera **100% local**, garantizando que tus datos permanezcan en tu equipo sin depender de servicios en la nube. Puedes instalarlo, revisarlo y auditarlo libremente.

![Captura de pantalla](https://github.com/user-attachments/assets/9d87f335-de40-4c38-ba23-bdbf4aa1376a)

---

## 🌟 Funcionalidades Principales

ARKSTOCK integra todas las herramientas necesarias para la gestión diaria de un negocio en una sola interfaz fluida e intuitiva.

### 🏠 Panel de Control (Dashboard)
Una vista general del estado de tu negocio al instante.
- **Gráficos en tiempo real:** Visualiza ventas y ganancias recientes.
- **Accesos directos:** Navegación rápida a las funciones más usadas.
- **Indicadores Clave (KPIs):** Resumen de stock total, ventas del día y alertas.

### 🛒 Punto de Venta (POS)
El núcleo del sistema, diseñado para agilizar el proceso de cobro.
- **Búsqueda Inteligente:** Encuentra productos por código o nombre y clientes al instante.
- **Carrito de Compras:** Gestión dinámica de items, cantidades y eliminación rápida.
- **Descuentos Globales:** Aplica descuentos por porcentaje o monto fijo al total de la venta.
- **Múltiples Métodos de Pago:** Soporte para Efectivo, Tarjeta, QR y Transferencia.
- **Control de Series:** Registro obligatorio de números de serie para productos tecnológicos o garantizados.
- **Tickets Automáticos:** Generación e impresión inmediata de recibos en PDF (formato térmico 80mm).

### 📦 Gestión de Inventario
Control total sobre tus existencias.
- **Catálogo de Productos:** Alta, baja y modificación de productos con soporte para tallas, colores y categorías.
- **Alertas de Stock Bajo:** Notificaciones automáticas cuando un producto alcanza su nivel mínimo.
- **Historial de Precios:** Rastreo de variaciones en costos de compra y precios de venta.
- **Ajustes de Inventario:** Registro de mermas, pérdidas o correcciones manuales de stock.

### 👥 Gestión de Terceros
- **Clientes:** Base de datos con historial de compras e información de contacto.
- **Proveedores:** Registro detallado para facilitar la reposición de mercadería.

### 💰 Caja y Finanzas
- **Corte de Caja (Arqueo):** Cierre diario con cálculo automático de efectivo esperado vs. real.
- **Historial de Ventas:** Consulta detallada de transacciones pasadas con filtros por fecha y usuario.

### 📄 Reportes y Documentos
- Generación de reportes detallados en PDF para auditorías o contabilidad.
- Exportación de listados de inventario y ventas.

---

## 📥 Contenido del Paquete de Instalación

El instalador oficial de la versión v1.0.12 contiene **exactamente** los siguientes archivos para una instalación automatizada:

- `setup.ps1` (Script de configuración)
- `programa/` (Carpeta con los binarios de la aplicación)
- `run_setup.bat` (Lanzador del instalador)

![Captura de pantalla](https://github.com/user-attachments/assets/7e8c9c6f-0007-4d38-ac4f-b4f29b6e782b)

> *No incluye archivos adicionales innecesarios.*

---

## 🚀 Instrucciones de Instalación

1. **Descargar** el archivo ZIP del último release.
2. **Extraer** todo el contenido en una carpeta de tu preferencia.
3. **Ejecutar** el archivo `run_setup.bat` (doble clic).
4. El instalador configurará el sistema automáticamente.
5. Al finalizar, busca **ARKSTOCK** en tu menú de inicio de Windows.
6. ¡Listo! Ya puedes gestionar tu negocio.

---

## 💻 Requisitos Mínimos

Para garantizar un funcionamiento fluido, tu equipo debe cumplir con lo siguiente:

| Componente | Requisito Mínimo |
| :--- | :--- |
| **Sistema Operativo** | Windows 10 (versión 1809+) o Windows 11 |
| **Memoria RAM** | 2 GB |
| **Procesador** | CPU estándar de 64 bits o 32 bits |
| **Almacenamiento** | 300 MB de espacio libre |
| **Dependencias** | Ninguna (Todo incluido para ejecución local) |

---

## 🏗️ Arquitectura y Tecnología

**ARKSTOCK** ha sido construido utilizando las últimas tecnologías de Microsoft para aplicaciones de escritorio, asegurando rendimiento y estética moderna.

### Stack Tecnológico
- **Framework:** .NET 8
- **Interfaz (UI):** WinUI 3 (Windows App SDK)
- **Lenguaje:** C#
- **Base de Datos:** Microsoft SQL Server (LocalDB / Cliente SQL integrado)
- **Reportes:** QuestPDF (Generación de tickets y reportes PDF)

### Características Técnicas
- **Patrón MVVM:** Separación clara entre la lógica de negocio y la interfaz de usuario.
- **Seguridad:** Hash de contraseñas con `BCrypt.Net` y gestión segura de conexiones.
- **Robustez:**
  - Control de instancia única (Mutex) para evitar múltiples ejecuciones.
  - Manejo global de excepciones y sistema de notificaciones Toast nativas.
- **Interfaz Fluida:** Uso de `MicaController` para efectos visuales nativos de Windows 11 y controles optimizados (`DataGrid` con virtualización).
- **Asistencia IA:** Aproximadamente el **60%** del código y funciones han sido optimizados o asistidos por Inteligencia Artificial para maximizar la eficiencia.

---

## 🛡️ Seguridad y Auditoría

Al ser un proyecto de **Código Libre**, cualquier persona puede revisar el código fuente para verificar su seguridad.
- **Sin telemetría oculta.**
- **Sin conexión obligatoria a internet.**
- **Base de datos local:** Tus clientes, ventas e inventario nunca salen de tu computadora.

---

**ARK DEV SYSTEM** - *Innovación y Código para la Comunidad.*
