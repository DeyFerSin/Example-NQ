# 🔧 Example NQ-EP4L Modbus/TCP Reader + Scanner (VB.NET)

[![VB.NET](https://img.shields.io/badge/VB.NET-512BD4?style=flat-square&logo=visualbasic&logoColor=white)](#)
[![Open Source](https://img.shields.io/badge/Open%20Source-%E2%9D%A4-blue?style=flat-square&logo=open-source-initiative&logoColor=white)](#)
[![MIT License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)
[![.NET 6+](https://img.shields.io/badge/.NET-6%2B-512BD4?style=flat-square&logo=dotnet&logoColor=white)](#)
[![NuGet NModbus](https://img.shields.io/nuget/v/NModbus4.svg?style=flat-square&logo=nuget&label=NModbus)](https://www.nuget.org/packages/NModbus)
[![GitHub Release](https://img.shields.io/github/v/release/DeyFerSin/Example-NQ?style=flat-square&display_name=tag&sort=semver)](https://github.com/DeyFerSin/Example-NQ/releases)

Este repositorio contiene **dos ejemplos en Visual Basic .NET** para comunicarse con el **IO‑Link Master NQ‑EP4L** mediante **Modbus/TCP** y un **sensor Keyence LR‑X100C** conectado al puerto 1:

1) **Reader simple** – Lee directamente el **Current Value** desde un registro ya conocido.  
2) **Scanner Modbus/TCP** – Recorre **varios Holding e Input Registers** para **descubrir** dónde publica el Master el valor de proceso (PV).

> ⚙️ Estos ejemplos son **educativos** para personas que desean comunicarse con este tipo de dispositivos industriales. No son una implementación oficial.

---

## 🧩 Dispositivos utilizados

| Dispositivo | Marca | Descripción |
| --- | --- | --- |
| **NQ‑EP4L** | Keyence | IO‑Link Master (4 puertos) con Ethernet industrial (Modbus/TCP o EtherNet/IP). |
| **LR‑X100C** | Keyence | Sensor láser de desplazamiento IO‑Link (rango 0–95.5 mm). |

El NQ‑EP4L actúa como **gateway Ethernet ⇄ IO‑Link** y expone el *process data* de cada puerto en registros (según su mapeo).

<img width="1470" height="661" alt="image" src="https://github.com/user-attachments/assets/f831e9f2-fb6e-4c22-ad0b-e0a7ea08ddae" />

---

## 📦 Dependencias

- **Visual Studio 2022+**
- **.NET Framework 4.8** o **.NET 6+**
- NuGet: [`NModbus`](https://www.nuget.org/packages/NModbus)

Instalación rápida:

```powershell
Install-Package NModbus
```

---

## 1) 💻 Reader simple (Current Value ya conocido)

Este lector asume que ya conocemos dónde está el PV. En pruebas reales con NQ‑EP4L+LR‑X, el **Current Value** quedó en **Holding Register @2** como **INT16** con **escala 0.1 mm** (869 ⇒ 86.9 mm).

```vbnet
Imports System.Net.Sockets
Imports NModbus

Module Module1
    Private Const MASTER_IP As String = "169.254.235.1"
    Private Const MASTER_PORT As Integer = 502
    Private Const UNIT_ID As Byte = 1
    Private Const PV_ADDR As UShort = 2 'Current Value confirmado

    Sub Main()
        Try
            Using client As New TcpClient()
                client.ReceiveTimeout = 3000
                client.SendTimeout = 3000
                client.Connect(MASTER_IP, MASTER_PORT)

                Dim factory = New ModbusFactory()
                Dim master = factory.CreateMaster(client)

                Dim regs = master.ReadHoldingRegisters(UNIT_ID, PV_ADDR, 1)
                Dim raw As Integer = regs(0)            'Si necesitas signo: CShort(regs(0))
                Dim mm As Double = raw / 10.0           'Escala 0.1 mm

                Console.WriteLine($"Current Value: {mm:0.1} mm (raw={raw})")
            End Using
        Catch ex As Exception
            Console.WriteLine("Error: " & ex.Message)
        End Try

        Console.WriteLine("ENTER para salir...")
        Console.ReadLine()
    End Sub
End Module
```

---

## 2) 🔎 Scanner Modbus/TCP (descubrir el registro correcto)

El **scanner** recorre **varios Holding e Input Registers** para **detectar candidatos** al PV. Funciona así, *tal cual lo hace el programa*:

1. **Rango de direcciones** configurable (por defecto `0..199`).  
2. Lee **bloques** (p.ej., 60 registros) como **Holding** y luego como **Input**.  
3. Realiza **dos lecturas** separadas por **300 ms** del mismo bloque.  
4. Reporta solo lo **interesante**:
   - **Registros no‑cero** o que **cambian** entre lecturas (indicando actividad).  
   - Interpreta cada dirección como **INT16 escalado x0.1 mm**.  
   - También intenta **FLOAT32** usando **dos registros** consecutivos (**a,a+1**), **probando ambos órdenes de palabras** (normal y *swap words*), porque algunos equipos invierten el orden Modbus.
5. Imprime líneas como:
   - `Holding @2: INT16 869 (~86.9 mm)`  
   - `Input @68-69: FLOAT32 59.700`

> El valor útil aparece como: **`Holding @2: INT16 869 (~86.9 mm)`**, lo que confirma que el PV está en **Holding 2** con escala 0.1 mm.

### Código del scanner (resumen)

> **Requiere** `NModbus`. El código completo de referencia debe incluirse en tu repo.

```vbnet
'Parámetros clave del escaneo
Private Const MASTER_IP As String = "169.254.235.1"
Private Const MASTER_PORT As Integer = 502
Private Const UNIT_ID As Byte = 1
Private Const START_ADDR As UShort = 0
Private Const END_ADDR As UShort = 199
Private Const BLOCK_SIZE As UShort = 60
Private Const CHANGE_THRESHOLD_INT As Integer = 1    '1 cuenta ~ 0.1 mm
Private Const CHANGE_THRESHOLD_FLOAT As Single = 0.01F
' ... (lectura de Holding/Input, doble pasada, comparación y prints)
```

### ¿Cómo interpretar la salida?

- **`Holding @2: INT16 869 (~86.9 mm)`**  
  - `Holding`: el tipo de registro (también se muestra `Input` si aplica).  
  - `@2`: dirección Modbus.  
  - `INT16 869`: valor crudo de 16 bits.  
  - `(~86.9 mm)`: valor interpretado con **escala x0.1 mm**.

- **`Input @68-69: FLOAT32 59.700`**  
  - `Input`: fue leído del espacio de **Input Registers**.
  - `@68-69`: se usaron **dos registros** (float de 32 bits).  
  - `FLOAT32`: interpretación IEEE‑754.  
  - Puede aparecer `FLOAT32(sw)` si se detecta el valor con **orden de palabras invertido**.

### ¿Para qué sirve?

- Cuando **no conoces** el mapeo del Master, mueves el objeto frente al LR‑X y **observas qué direcciones cambian**.  
- La **dirección** donde veas variación coherente con la medición es la que usarás después en el **Reader simple**.

---
## ✅ Prueba de integración y validación de lectura

![Example NQ](https://github.com/user-attachments/assets/cd41c398-57cf-42a2-a449-4cd4e7f838e1)

**Resultados observados**  
En la consola (ver captura) se imprimieron líneas como:

- `Holding @2: INT16 867->868 (~86.7->86.8 mm)`  
- `Input  @2: INT16 867 (~86.7 mm)`

En paralelo, el **display del LR-X** mostró **86.7 mm**.

**Interpretación**
- El registro **Holding @2** contiene el *Current Value* del port 1 con **escala 0.1 mm**.  
  - Relación usada: `mm = raw / 10` → `867 / 10 = 86.7 mm`.  
- El valor también aparece en **Input @2** en este master (sombra/copia del PV). Dependiendo de la configuración del NQ-EP4L, el PV puede exponerse en Holding, Input o ambos; por ello el **scanner** es útil para **descubrir** el mapeo real en campo.
- Otros registros constantes (p. ej., `4 (~0.4 mm)`) no corresponden a la variable de proceso y pueden ser marcadores/estado internos.

**Conclusión**
- Existe **correlación 1:1** entre el valor del display del LR-X y la lectura Modbus/TCP del **Holding @2**.  
- El **reader simple** del proyecto puede fijarse en esa dirección para obtener el *Current Value* en mm de forma fiable.

---

## 🆕 Actualización — Example NQ v1.0.1

**Resumen:** soporte de **INT16 con signo** y corrección de **overflow** en *Scanner* y *Reader* para lecturas negativas.

### ✨ Qué cambió
- Los registros Modbus de 16 bits ahora se **reinterpretan como `Int16`** (complemento a dos) en lugar de `UShort`.
- Nueva función utilitaria **`UShortToInt16`** para reinterpretar bits y evitar `OverflowException`.
- Conversión a milímetros con **signo**: `mm = raw * 0.1` (`raw As Short`).
- La detección de cambios en el *scanner* se hace usando **valores con signo** (negativos/positivos).
- Se añadió un **filtro de plausibilidad** para lecturas `FLOAT32` fuera de rango (ruido).

### ✅ Resultado esperado (ejemplo)
```text
Escaneando HOLDING REGISTERS...
Holding @2: INT16  -175  (-17.5 mm)
Holding @2: INT16  -174->-173  (-17.4 -> -17.3 mm)
Holding @2: INT16   52   (+5.2 mm)

Escaneando INPUT REGISTERS...
Input  @2: INT16  -180  (-18.0 mm)
Input  @2: INT16   867  (+86.7 mm)
```

### 🔎 Por qué
Al alejar el objetivo, el sensor puede entregar **valores negativos**; el *cast* anterior provocaba **desbordamientos** y lecturas incorrectas. Reinterpretar como `Int16` preserva el signo y hace la lectura **consistente**.

### 🔄 Impacto y compatibilidad
- *Scanner*: ya no se cae por overflow y reporta mm con signo.
- *Reader*: muestra el valor real en mm, sea negativo o positivo.
- **Retrocompatible** con mapeos previos (mejora de robustez).

### Caso 1 — Positivo (funcionamiento normal)
![Positivo](https://github.com/user-attachments/assets/0229e22f-a84e-462f-b83d-281ee459f40e)  
*El PV positivo se muestra como antes: conversión directa `mm = raw * 0.1` y coincidencia con el display del sensor.*

### Caso 2 — Negativo (motivo de la actualización)
![Negativo](https://github.com/user-attachments/assets/245c7027-bf65-4b42-85f2-fbd1b8d38ad9)  
*El PV negativo ahora se interpreta correctamente como `Int16` con signo. Se elimina el **Overflow** y la lectura aparece en milímetros con signo (p. ej., `-19.4 mm`).*

---

## 🔐 Notas y consejos

- Verifica que el NQ‑EP4L tenga **Modbus/TCP habilitado** y el firewall permita el puerto **502**.
- Si **nada cambia** o sólo ves ceros, tu Master podría estar publicando datos **por EtherNet/IP (CIP)**. En ese caso debes leer el **Assembly Object (Class 0x04, Instance 0x64/Attr 0x03 para port 1)** mediante CIP.
- La **escala** (x0.1 mm) y la **dirección** pueden variar según configuración; el scanner te ayuda a confirmarlas.

---

## 🧰 Posibles mejoras

- Loop de lectura continua con timestamp y guardado en **CSV**.  
- Interfaz **WinForms/WPF** (botón “Leer” y textbox con mm).  
- Selección de **puerto IO‑Link** y mapeos por puerto.  
- Lectura por **EtherNet/IP (CIP)** como alternativa.


Código abierto bajo **MIT**: úsalo, modifícalo y compártelo con atribución.

---

> *“Compartir conocimiento técnico impulsa la innovación.”*
