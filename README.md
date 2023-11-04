# AutoMark
This is a proof of concept that I wrote. It is designed to be able to take scanned versions of homework assignments written by students, run OCR (optical character recognition) on the student's answers, and verify if they are correct or not.

## Usage and features

Upon running the program, it will take a scanned homework assignment, extract the written answers out of it (using a blank homework assignment template and specified answer details/coordinates), send them off to an Azure Computer Vision instance, receive text versions of the answers, and verify whether they are correct or not.

This program requires four files in its working directory. Out of this only being a proof of concept, they require certain hard-coded names:

* ``boundaries2.png`` - a blank copy of the homework assignment
* ``scan.png`` - a filled-in version of the above homework assignment
* ``coords.csv`` - a file specifying the details of the answers that need to be filled in (the answer itself, pixel-coordinates with respect to ``boundaries2.png``, etc...)
* ``convincer.png`` - An image file containing sample numeric data, whose use is described in the technicalities section.

Sample copies of each of these files can be found in the ``example`` folder. In addition, this repository was uploaded with all hard-coded private keys removed from the code. The program uses Microsoft Azure's OCR service and specifically needs the following:

* ``ocr.cs`` - This file needs a valid Microsoft Azure "Computer Vision" resource endpoint url and access key specified near the start.

It is worth noting that most of the software engineering challenges lay in the OCR process. In theory, from here, it should not be too difficult to implement certain question types more sophisticated than simply verifying whether the written answer corresponds to a pre-set value. Matrix Gauss-Jordan elimination would be one such example, which are often very tedious to mark for humans (especially if a mistake exists somewhere in the computation), but as long as a computer can be given an accurate set of step-by-step computations that the student wrote, verifying correctness and spotting mistakes would be trivial.

## Interesting technicalities that needed solving

It is worth noting that this project came with certain technicalities in the image extraction and OCR phases that needed a bit of work to solve. The following are the main ones:

* Multiple scans of homework will never have the same exact alignment, etc... when scanning. This program first manually aligns the scanned homework with a fixed blank copy provided.
* As far as I could tell (when this program was written at least), Azure's Computer Vision had no way of being specified whether the text to be scanned should be interpreted as letters from the alphabet or numerical digits. Many times, something like the number 1 would be interpreted as the letter I (capital i) or l (lowercase L), for example. A solution to this is to manually create a larger image with, for example, the number 34 pasted to the left and right of the original image. For example, an image of a (handwritten) 1 becomes an image of 34134, which "encourages" the OCR AI to interpret the whole thing as numerical. Then the scanned text just needs to have the extra "34" trimmed off of both ends. This is what the ``convincer.png`` specified earlier is for. It may seem janky, but it makes the accuracy go from "sort of works" to "near perfect".
* Azure's Computer Vision has pricing based on the number of API calls made (and the free trial works similarly). As such, scanning many tiny images (cutouts of each answer box) separately is actually quite inefficient, while it would also be quite wasteful to scan the entire homework page at once. The solution implemented is to cut out the answer from each box, paste all of them into a giant "collage" image of all the handwritten answers (with appropriate separation in between), and send that single image off to be scanned. This way, as many answers as possible can be scanned with a single API call.
