# VirtualPaper: Datenschutz, KI-Verarbeitung und Urheberrecht

Dokumentversion: 1.0. Aktualisiert: 2026-11-15. Herausgeber und Projektbetreuer: PaperHammer.

Diese Hinweise erläutern, wie die EXE- und Microsoft-Store-Ausgaben von VirtualPaper Daten verarbeiten und welche Rechte bei der Nutzung von KI-Funktionen und mitgelieferten Inhalten zu beachten sind. Für externe Websites und Dienste gelten deren eigene Richtlinien.

<a id="privacy-policy"></a>
## Datenschutzerklärung

### Lokale Dateien und Einstellungen

VirtualPaper liest die von Ihnen ausgewählten Bilder, Videos, animierten Bilder und Webprojekte, um sie anzuzeigen, zu bearbeiten oder zu verarbeiten. Hintergrund-Metadaten, Vorschaubilder, Favoriten, Tags, Effektvorlagen, zuletzt verwendete Projektpfade, Einstellungen und Monitoranordnungen werden lokal gespeichert. Ein VirtualPaper-Konto ist nicht erforderlich. Für KI-Inferenz, Werbung oder Verhaltensanalyse werden diese Inhalte und Aufzeichnungen nicht an den Projektbetreuer hochgeladen.

Der Standardordner ist `%LOCALAPPDATA%\VirtualPaper`. Windows kann Daten paketierter Anwendungen umleiten. Hintergrundbibliotheken können den in den Einstellungen gewählten Speicherort verwenden; exportierte Projekte und Bilder bleiben am von Ihnen gewählten Ort. Vorschau, Rückgängig-Funktion, Tiefenkarten und Browserdaten können weitere lokale Dateien erzeugen. Diese Dateien sind keine Cloud-Sicherung.

### Diagnose und Fehlerberichte

Lokale Protokolle können Anwendungs- und Windows-Versionen, Anzeigesprache, CPU-/GPU-Informationen, Fehler, Dateipfade und Wiedergabedetails enthalten. Der Protokollexport erstellt eine lokale ZIP-Datei mit Protokollen, Benutzereinstellungen und Hintergrundanordnungen. Sie wird nicht automatisch an PaperHammer gesendet. Prüfen Sie das Archiv vor dem Teilen: Pfade können Ihren Windows-Benutzernamen oder private Projektnamen offenlegen. Freiwillig auf GitHub veröffentlichte Berichte und Anhänge werden von GitHub verarbeitet und können öffentlich sein.

### Standort

Bei aktivierter Tageszeitanpassung fordert die Anwendung die Windows-Standortberechtigung an und berechnet Sonnenaufgang und Sonnenuntergang lokal anhand von Breiten- und Längengrad. Ohne Zugriff wird ein Standardstandort verwendet; die Beleuchtung passt dann möglicherweise nicht zu Ihrer Region. Deaktivieren Sie die Funktion, um deren Standortanfragen zu beenden. Die Berechtigung lässt sich auch unter „Datenschutz und Sicherheit“ in Windows verwalten. VirtualPaper sendet diese Koordinaten für die Berechnung nicht an den Projektbetreuer.

### Netzwerk und Drittanbieter

Die EXE-Ausgabe kann GitHub und Downloadserver kontaktieren, um Anwendungs- oder Plugin-Updates zu prüfen und herunterzuladen. Bei der Installation können Microsoft-Laufzeitkomponenten geladen werden. Store-Verteilung und -Updates übernimmt Microsoft. Netzwerkempfänger erhalten Verbindungsinformationen wie IP-Adresse und Anfrageheader. Hilfe-, Feedback- und Quellcodelinks führen zur jeweiligen Website. Webhintergründe und importierte Webprojekte können selbstständig externe Skripte, Medien oder Dienste ihrer Autoren laden.

WebView2-Diagnose und SmartScreen senden Informationen an Microsoft; siehe [Microsoft-Datenschutzerklärung](https://privacy.microsoft.com/privacystatement). Einige Berichte folgen den Windows-Diagnoseeinstellungen. Diese Vorgänge sind von der lokalen KI-Inferenz in VirtualPaper getrennt.

### Aufbewahrung, Sicherheit und Ihre Möglichkeiten

Gespeicherte Einstellungen, Bibliotheken, Projekte, Tiefen-Caches und Browserprofile bleiben lokal, bis Sie oder eine einschlägige Bereinigungs- oder Deinstallationsfunktion sie entfernen. Temporäre KI- oder Bearbeitungsergebnisse können beim nächsten Start gelöscht werden. Speichern Sie gewünschte Ergebnisse oder fügen Sie sie der Bibliothek hinzu. Nicht alle Protokolle und Caches haben eine feste automatische Löschfrist. Eine Deinstallation entfernt nicht zwingend verschobene Bibliotheken oder exportierte Dateien.

Sie können lokale Dateien einsehen, exportieren, berichtigen oder löschen und die zugehörigen Einstellungen ändern. Beenden Sie vor manueller Datenlöschung VirtualPaper und seine Hintergrundprozesse und sichern Sie benötigte Dateien. Lokale Daten werden durch Windows-Konto- und Dateisystemberechtigungen geschützt; VirtualPaper bietet keinen gesonderten verschlüsselten Datentresor. Verwenden Sie vertrauenswürdige Hintergrundprojekte und schützen Sie Ihr Windows-Konto.

### Kontakt und Änderungen

Bei Datenschutzfragen oder Anliegen zu freiwillig übermittelten Informationen erreichen Sie PaperHammer über [GitHub Issues](https://github.com/PaperHammer/VirtualPaper/issues). Veröffentlichen Sie keine sensiblen Daten; fragen Sie zunächst nach einer geeigneten Übermittlungsmöglichkeit. Für Informationen bei GitHub, Microsoft oder externen Hintergrunddiensten gelten deren eigene Kontrollmöglichkeiten. Bei relevanten Änderungen der Anwendung wird dieser Hinweis aktualisiert. Version und Datum kennzeichnen die mitgelieferte Fassung.

<a id="ai-processing-and-rights"></a>
## KI-Verarbeitung und Rechte

Tiefenschätzung, Stilübertragung und Bildverbesserung nutzen Modelle auf Ihrem Computer und je nach Funktion und Einstellung CPU/GPU. Diese Inferenz ruft keinen Cloud-KI-Dienst auf; die Modelldateien müssen lokal vorliegen. Qualität und Geschwindigkeit hängen von Ausgangsmaterial, Modell und Hardware ab. Erzeugte Details können ungenau sein; eine Tiefenkarte ist eine Schätzung, keine Messung.

Die Bearbeitung mit KI verleiht Ihnen nicht allein dadurch Urheberrechte am Ausgangsmaterial oder Ergebnis und erlaubt auch nicht automatisch die Nutzung fremder Inhalte. Ob ein Ergebnis urheberrechtlich geschützt ist und wem die Rechte zustehen, hängt vom anwendbaren Recht, der Lizenz des Ausgangsmaterials und menschlichen schöpferischen Beiträgen ab. Bestehende Nutzungsbeschränkungen entfallen durch die KI-Bearbeitung nicht automatisch. Vergewissern Sie sich vor einer Veröffentlichung, Weitergabe oder kommerziellen Nutzung, dass Sie die erforderlichen Rechte oder Erlaubnisse besitzen. Urheber-, Persönlichkeits-, Marken- und andere Rechte Dritter können weiterhin zu beachten sein.

VirtualPaper beansprucht nicht allein durch Ihre Nutzung der Anwendung Rechte an Ihren Eingaben oder Ergebnissen. Die Open-Source-Lizenz der Software stellt Ihre Inhalte oder Ausgaben nicht automatisch unter die GPL. Modell-/Laufzeitlizenzen und Bildrechte sind getrennt zu betrachten; ein kostenloses Modell garantiert keine uneingeschränkt nutzbaren Ausgaben. VirtualPaper garantiert weder Rechtefreiheit noch Exklusivität oder kommerzielle Eignung. Dieser Hinweis ersetzt keine erforderlichen Erlaubnisse und schränkt gesetzliche Rechte nicht ein.

Einige mitgelieferte Landschaftsbilder wurden bei der Erstellung der Sammlung mit der OpenAI-Bildgenerierung erzeugt. Das ist von der lokalen Inferenz der Anwendung getrennt und bedeutet nicht, dass Ihre Bilder an OpenAI gesendet werden. Die Einzelhinweise nennen Herkunft und Nutzungsbedingungen; an diesen KI-Bildern wird kein ausschließliches Urheberrecht beansprucht.

<a id="copyright-and-licenses"></a>
## Urheberrecht und Lizenzen

Der Anwendungscode von VirtualPaper wird unter der [GNU GPL Version 3](../licenses/GPL-3.0.txt) verbreitet. Quellcode und Veröffentlichungen finden Sie im [VirtualPaper-Repository](https://github.com/PaperHammer/VirtualPaper). Gewährleistungs- und Weitergabebedingungen stehen in der Lizenz; zwingende gesetzliche Rechte bleiben unberührt.

Für Drittanbieterkomponenten und Modellgewichte gelten die jeweiligen Urheberrechtshinweise und Lizenzen. Einzelheiten finden Sie in den [Drittanbieterinformationen](../THIRD-PARTY.txt) sowie den beigefügten Original-Lizenzen und NOTICE-Dateien. Übersetzungen und Erläuterungen dienen der Information; sie ersetzen weder die Originalbedingungen noch erteilen sie eine pauschale Erlaubnis zur Nutzung fremder Inhalte.

Mitgelieferte Hintergründe haben eigene Herkunfts- und Nutzungsangaben. Das Originalvideo Santa Rosa Waves stammt von Blervis, das Ausgangsmaterial von Bigora Cascade von Termininja; die mitgelieferten Bearbeitungen stehen unter CC0. Weitere Hinweise nennen VirtualPapers erzeugte Bilder oder Animationscode. Bewahren Sie bei Weitergabe die jeweilige `NOTICE.txt` und die zugehörige Lizenz auf. Herausgeber- oder Bearbeiternennungen ersetzen weder die ursprünglichen Urheberangaben noch ändern sie die ursprüngliche Lizenz.

CC0 verlangt keine Namensnennung; Urheber- und Quellenangaben dienen der Herkunftsdokumentation und fügen keine Weitergabebedingung hinzu.
