Este proyecto debe desarrollarse siguiendo estrictamente el archivo:

PRD_Smart_Document_Control_SAP_B1.md

Reglas obligatorias:

1. Antes de escribir código, revisar el PRD completo.
2. No desarrollar funcionalidades fuera del MVP sin confirmación explícita.
3. No eliminar criterios de aceptación definidos en el PRD.
4. No usar DI API.
5. No modificar documentos SAP por SQL directo.
6. Todo cierre documental debe hacerse vía SAP Business One Service Layer.
7. Mantener arquitectura por capas:
   - Domain
   - Application
   - Infrastructure
   - Runner
   - Tests
8. Si existe ambigüedad funcional o técnica, detenerse y preguntar.
9. Después de cada fase, validar avance contra el PRD.
10. El primer objetivo es simulación confiable en TST, no cierre real en PRD.