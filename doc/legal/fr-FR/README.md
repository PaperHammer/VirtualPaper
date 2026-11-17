# VirtualPaper : confidentialité, traitement par IA et droits d’auteur

Version du document : 1.0. Mise à jour : 2026-11-15. Éditeur et responsable du projet : PaperHammer.

Ce document explique comment les éditions EXE et Microsoft Store de VirtualPaper traitent les données et quels droits prendre en compte lors de l’utilisation des fonctions d’IA et des contenus intégrés. Les sites et services externes disposent de leurs propres politiques.

<a id="privacy-policy"></a>
## Politique de confidentialité

### Fichiers et paramètres locaux

VirtualPaper lit les images, vidéos, images animées et projets Web que vous choisissez afin de les afficher, modifier ou traiter. Les métadonnées, miniatures, favoris, étiquettes, préréglages d’effets, chemins des projets récents, paramètres et dispositions des écrans sont conservés localement. Aucun compte VirtualPaper n’est nécessaire. Ces contenus et enregistrements ne sont pas envoyés au responsable du projet pour l’inférence IA, la publicité ou l’analyse comportementale.

Le dossier par défaut est `%LOCALAPPDATA%\VirtualPaper`. Windows peut rediriger les données des applications empaquetées. Les bibliothèques peuvent utiliser l’emplacement choisi dans les paramètres ; les projets et images exportés restent à l’emplacement sélectionné. L’aperçu, l’annulation, les cartes de profondeur et les données du navigateur peuvent créer d’autres fichiers locaux. Ils ne constituent pas une sauvegarde dans le cloud.

### Diagnostic et signalements

Les journaux locaux peuvent contenir les versions de l’application et de Windows, la langue d’interface, des informations CPU/GPU, des erreurs, des chemins de fichiers et des détails de lecture. L’exportation crée un ZIP local avec les journaux, les paramètres utilisateur et les dispositions des fonds d’écran. Il n’est pas envoyé automatiquement à PaperHammer. Vérifiez-le avant de le partager : les chemins peuvent révéler votre nom d’utilisateur Windows ou des noms de projets privés. Les signalements et pièces jointes publiés volontairement sur GitHub sont traités par GitHub et peuvent être publics.

### Localisation

Lorsque l’adaptation à l’heure est activée, l’application demande l’autorisation de localisation Windows et utilise localement latitude et longitude pour calculer le lever et le coucher du soleil. Sans accès, une position par défaut est utilisée ; l’éclairage peut ne pas correspondre à votre région. Désactivez cette fonction pour arrêter ses demandes de localisation. Vous pouvez aussi gérer l’autorisation dans les paramètres de confidentialité et de sécurité Windows. VirtualPaper n’envoie pas ces coordonnées au responsable du projet pour ce calcul.

### Réseau et services tiers

L’édition EXE peut contacter GitHub et des serveurs de téléchargement pour rechercher et obtenir des mises à jour de l’application ou des modules. L’installation peut télécharger des composants d’exécution Microsoft. La distribution et les mises à jour du Store sont gérées par Microsoft. Les requêtes exposent au destinataire des informations de connexion, comme votre adresse IP et les en-têtes. Ouvrir les liens d’aide, de retour ou de code source connecte au site concerné. Les fonds d’écran et projets Web importés peuvent charger indépendamment des scripts, médias ou services distants contrôlés par leurs auteurs.

Les diagnostics WebView2 et SmartScreen envoient des informations à Microsoft ; consultez sa [déclaration de confidentialité](https://privacy.microsoft.com/privacystatement). Certains rapports suivent les paramètres de diagnostic Windows. Ces mécanismes sont distincts de l’inférence IA locale de VirtualPaper.

### Conservation, sécurité et choix

Les paramètres, bibliothèques, projets, caches de profondeur et profils de navigateur enregistrés restent locaux jusqu’à leur suppression par vous ou par une opération de nettoyage ou de désinstallation applicable. Les résultats temporaires d’IA ou d’édition peuvent être effacés au prochain démarrage ; enregistrez-les ou ajoutez-les à la bibliothèque pour les conserver. Tous les journaux et caches ne disposent pas d’un délai fixe de suppression automatique. La désinstallation ne garantit pas la suppression des bibliothèques déplacées ou des fichiers exportés.

Vous pouvez consulter, exporter, corriger ou supprimer vos fichiers locaux et modifier les paramètres concernés. Avant une suppression manuelle, fermez VirtualPaper et ses processus en arrière-plan, puis sauvegardez les fichiers à conserver. Les données locales reposent sur les protections du compte Windows et du système de fichiers ; VirtualPaper ne fournit pas de coffre chiffré distinct. Choisissez des projets de confiance et protégez votre compte Windows.

### Contact et modifications

Pour les questions de confidentialité ou les demandes concernant des informations volontairement transmises au projet, contactez PaperHammer via [GitHub Issues](https://github.com/PaperHammer/VirtualPaper/issues). Ne publiez pas d’informations sensibles ; demandez d’abord un moyen approprié de les transmettre. Les informations détenues par GitHub, Microsoft ou un service externe d’un fond d’écran relèvent des contrôles de ce fournisseur. Ce texte sera mis à jour si le comportement pertinent change ; sa version et sa date identifient la copie fournie avec l’application.

<a id="ai-processing-and-rights"></a>
## Traitement par IA et droits

L’estimation de profondeur, le transfert de style et l’amélioration d’image utilisent des modèles sur votre ordinateur, avec le CPU/GPU selon la fonction et les paramètres. Ces inférences n’appellent pas de service IA dans le cloud. Les modèles doivent être disponibles localement. Qualité et vitesse dépendent de la source, du modèle et du matériel ; les détails créés peuvent être inexacts et la profondeur est une estimation, pas une mesure.

Le traitement par IA ne vous confère pas, à lui seul, de droits d’auteur sur le contenu source ou le résultat, ni l’autorisation d’utiliser les contenus d’autrui. La protection du résultat et l’identité du titulaire des droits dépendent du droit applicable, de la licence du contenu source et des contributions créatives humaines. Les restrictions existantes ne disparaissent pas automatiquement après un traitement par IA. Avant toute publication, redistribution ou utilisation commerciale, assurez-vous de disposer des droits ou autorisations nécessaires. Les droits d’auteur, le droit à l’image, les droits sur les marques et les autres droits de tiers restent à prendre en compte.

VirtualPaper ne revendique pas la propriété de vos entrées ou résultats du seul fait de l’utilisation de l’application. La licence libre du logiciel ne place pas automatiquement vos contenus ou sorties sous GPL. Les licences des modèles et composants d’exécution sont distinctes des autorisations des images ; un modèle gratuit ne garantit pas des résultats utilisables sans restriction. VirtualPaper ne garantit ni absence d’atteinte aux droits, ni exclusivité, ni aptitude commerciale. Cette information ne remplace pas les autorisations nécessaires et ne limite pas vos droits légaux.

Certains paysages intégrés ont été créés avec la génération d’images OpenAI lors de la préparation de la collection. Cela est distinct de l’inférence locale et ne signifie pas que vos images sont envoyées à OpenAI. Leurs notices individuelles précisent la provenance et les conditions d’utilisation ; aucun droit d’auteur exclusif n’est revendiqué sur ces images générées par IA.

<a id="copyright-and-licenses"></a>
## Droits d’auteur et licences

Le code de VirtualPaper est distribué sous [GNU GPL version 3](../licenses/GPL-3.0.txt). Le code source et les versions sont disponibles dans le [dépôt VirtualPaper](https://github.com/PaperHammer/VirtualPaper). La licence définit les conditions de garantie et de redistribution ; les droits légaux impératifs restent inchangés.

Les composants tiers et les poids des modèles sont soumis à leurs propres mentions de droits d’auteur et licences. Consultez les [informations sur les composants tiers](../THIRD-PARTY.txt), ainsi que les licences et fichiers NOTICE originaux fournis. Les traductions et explications sont proposées à titre informatif ; elles ne remplacent pas les conditions originales et n’accordent pas d’autorisation générale sur les contenus tiers.

Les fonds d’écran intégrés ont leurs propres provenances et conditions. La vidéo originale Santa Rosa Waves est de Blervis et les images sources de Bigora Cascade sont de Termininja ; les adaptations fournies sont sous CC0. Les autres notices identifient les images générées ou le code d’animation de VirtualPaper. Conservez le `NOTICE.txt` et les licences joints lors de la redistribution. Le crédit d’un éditeur ou d’un adaptateur ne remplace pas le créateur original et ne change pas la licence d’origine.

CC0 n’impose pas d’attribution ; les mentions de créateur et de source documentent la provenance sans ajouter de condition de redistribution.
